using System.Net;
using System.Text.Json;
using Defra.PTS.User.ApiServices.Interface;
using Defra.PTS.User.Models.CustomException;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Model = Defra.PTS.User.Models;
using Entity = Defra.PTS.User.Entities;

namespace Defra.PTS.User.Functions.Functions.User
{
    public class User(IUserService userService, IOwnerService ownerService, ILogger<User> logger)
    {
        private const string InvalidUserInputMessage = "Invalid user input, is NUll or Empty";

      [Function("CreateUser")]
        [OpenApiOperation(operationId: "CreateUser", tags: new[] { "User" }, Summary = "Create a new user", Description = "Creates a new user in the system")]
        [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(Model.User), Required = true, Description = "User data")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(Guid), Description = "User created successfully")]
        public async Task<HttpResponseData> CreateUser(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "createuser")] HttpRequestData? req)
        {
    if (req == null)
   {
      throw new UserFunctionException("Invalid user input, is NULL or Empty");
            }

       var inputData = req.Body ?? throw new UserFunctionException("Invalid user input, is NULL or Empty");
var userModel = await userService.GetUserModel(inputData) ?? throw new UserFunctionException("Failed to parse user model from input data");

        Guid userId;

if (HasValidContactId(userModel))
  {
                userId = await HandleContactIdBasedUser(userModel);
            }
else
       {
     userId = await HandleEmailBasedUser(userModel);
        }

var response = req.CreateResponse(HttpStatusCode.OK);
    await response.WriteAsJsonAsync(userId);
            return response;
        }

        private static bool HasValidContactId(Model.User userModel)
        {
          return userModel.ContactId.HasValue && userModel.ContactId.Value != Guid.Empty;
        }

        private async Task<Guid> HandleContactIdBasedUser(Model.User userModel)
        {
        var existingUser = await userService.GetUserByContactId(userModel.ContactId!.Value);

  if (existingUser == null)
            {
           return await HandleEmailBasedUser(userModel);
    }

        await ProcessEmailUpdate(existingUser, userModel);
    await UpdateSignInTime(userModel.Email);

 return existingUser.Id;
        }

    private async Task ProcessEmailUpdate(Entity.User existingUser, Model.User userModel)
    {
         var existingUserEmail = existingUser.Email;
            var newEmail = userModel.Email;
   if (!IsEmailChanged(existingUserEmail, newEmail))
            {
    return;
        }

   logger.LogInformation("Email changed for ContactId {ContactId} from {OldEmail} to {NewEmail}",
            userModel.ContactId, existingUserEmail, newEmail);

            try
       {
              // Free the new email if a different identity currently holds it, so this account can adopt it without colliding.
              await RetireConflictingEmailHolders(newEmail!, userModel);
              await userService.UpdateUserEmail(existingUserEmail!, newEmail!);
           await ownerService.UpdateOwnerEmailsByOldEmail(existingUserEmail!, newEmail!);
      logger.LogInformation("Successfully updated user and owner emails for ContactId {ContactId}", userModel.ContactId);
     }
catch (Exception ex)
            {
          logger.LogError(ex, "Failed to update emails for ContactId {ContactId}: {ErrorMessage}",
        userModel.ContactId, ex.Message);
        }
        }

        private static bool IsEmailChanged(string? existingEmail, string? newEmail)
        {
         return !string.IsNullOrEmpty(existingEmail) &&
     !string.IsNullOrEmpty(newEmail) &&
    !string.Equals(existingEmail, newEmail, StringComparison.OrdinalIgnoreCase);
        }

   private async Task UpdateSignInTime(string? email)
        {
       if (string.IsNullOrEmpty(email))
            {
           return;
        }

            try
            {
    await userService.UpdateUser(email, "signin");
    }
     catch (Exception ex)
       {
        logger.LogWarning(ex, "Failed to update sign-in time for user {Email}", email);
    }
        }

        private async Task<Guid> HandleEmailBasedUser(Model.User userModel)
        {
            if (string.IsNullOrEmpty(userModel.Email))
  {
            throw new UserFunctionException("User model must have either ContactId or Email");
            }

            var emailHolders = await userService.GetUsersByEmail(userModel.Email) ?? [];

            // Same GG identity already owns this email -> ordinary repeat sign-in.
            var sameIdentityUser = emailHolders.Find(holder => IsSameIdentity(holder, userModel));
            if (sameIdentityUser != null)
            {
                await UpdateSignInTime(userModel.Email);
                return sameIdentityUser.Id;
            }

            // Email is held only by a different identity (abandoned/re-registered account).
            // Retire the old holder's email to a reserved .invalid value and register a fresh NIPTS user,
            // so a new GG ID can never inherit the previous user's data.
            if (emailHolders.Count > 0)
            {
                await RetireConflictingEmailHolders(userModel.Email, userModel);
                logger.LogInformation("Creating new user for re-registered email {Email}", userModel.Email);
                return await userService.CreateUser(userModel);
            }

            logger.LogInformation("Creating new user for email {Email}", userModel.Email);
            return await userService.CreateUser(userModel);
        }

        /// <summary>
        /// Renames the email of every existing user whose identity differs from the incoming identity, moving it
        /// (and any owner records on that email) to a reserved ".invalid" value. Only the email changes, so an
        /// IDM->PETS sync keyed on ContactId/Uniquereference will not undo the change.
        /// </summary>
        private async Task RetireConflictingEmailHolders(string email, Model.User userModel)
        {
            var holders = await userService.GetUsersByEmail(email) ?? [];

            foreach (var holder in holders)
            {
                if (IsSameIdentity(holder, userModel))
                {
                    continue;
                }

                var retiredEmail = BuildRetiredEmail(email, holder);

                logger.LogWarning(
                    "Retiring email {Email} held by user {UserId} (Uniquereference {UniqueReference}, ContactId {ContactId}) to {RetiredEmail} due to re-registration by a different identity",
                    email, holder.Id, holder.Uniquereference, holder.ContactId, retiredEmail);

                await userService.RetireUserEmail(holder.Id, retiredEmail);
                await ownerService.UpdateOwnerEmailsByOldEmail(email, retiredEmail);
            }
        }

        /// <summary>
        /// Determines whether an existing user record represents the same person as the incoming request.
        /// A differing Uniquereference (GG ID) is the ONLY reliable evidence of a different person and is the
        /// sole trigger for a split. A differing ContactId is not: IDM legitimately re-issues ContactIds for the
        /// same person, so when Uniquereference cannot be compared the records are treated as the same person.
        /// </summary>
        private static bool IsSameIdentity(Entity.User existingUser, Model.User incoming)
        {
            if (!string.IsNullOrWhiteSpace(incoming.Uniquereference) &&
                !string.IsNullOrWhiteSpace(existingUser.Uniquereference))
            {
                return string.Equals(existingUser.Uniquereference, incoming.Uniquereference, StringComparison.OrdinalIgnoreCase);
            }

            // No comparable Uniquereference -> cannot prove a different person -> treat as the same account
            // (covers the ContactId-changed case and legacy rows created before Uniquereference was captured).
            return true;
        }

        private static string BuildRetiredEmail(string email, Entity.User holder)
        {
            var identityToken = !string.IsNullOrWhiteSpace(holder.Uniquereference)
                ? new string(holder.Uniquereference!.Where(char.IsLetterOrDigit).ToArray())
                : holder.Id.ToString("N");

            if (string.IsNullOrEmpty(identityToken))
            {
                identityToken = holder.Id.ToString("N");
            }

            // Append the ContactId held at retirement time so repeated re-registrations of the same
            // email produce distinct .invalid values and never collide.
            var contactToken = holder.ContactId.HasValue && holder.ContactId.Value != Guid.Empty
                ? holder.ContactId.Value.ToString("N")
                : holder.Id.ToString("N");

            // ".invalid" is a reserved TLD (RFC 2606) so this value can never match a real incoming email.
            return $"{email}.{identityToken}.{contactToken}.invalid";
        }


     /// <summary>
      /// Update User
        /// </summary>
  /// <param name="req"></param>
        /// <returns></returns>
   [Function("UpdateUser")]
        [OpenApiOperation(operationId: "UpdateUser", tags: new[] { "User" }, Summary = "Update user details", Description = "Updates an existing user's information")]
        [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(Model.UserEmail), Required = true, Description = "User email and type")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(Guid), Description = "User updated successfully")]
        public async Task<HttpResponseData> UpdateUser(
     [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "updateuser")] HttpRequestData? req)
     {
            if (req == null)
      {
          throw new UserFunctionException(InvalidUserInputMessage);
            }

            var inputData = req.Body ?? throw new UserFunctionException(InvalidUserInputMessage);
 var userEmailModel = await userService.GetUserEmailModel(inputData);

   var response = req.CreateResponse(HttpStatusCode.OK);

            if (await userService.DoesUserExists(userEmailModel.Email!))
   {
 var userId = await userService.UpdateUser(userEmailModel.Email!, userEmailModel.Type!);
     logger.LogInformation("User updated with ID: {0}", userId);
        await response.WriteAsJsonAsync(userId);
            }
            else
        {
      await response.WriteAsJsonAsync("Cannot update new User as user does not exists");
            }

     return response;
        }

      /// <summary>
     /// Update User Address
     /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        [Function("UpdateUserAddress")]
        [OpenApiOperation(operationId: "UpdateUserAddress", tags: new[] { "User" }, Summary = "Update user address", Description = "Updates a user's address information")]
        [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(Model.UserEmail), Required = true, Description = "User email and address ID")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(Guid), Description = "User address updated successfully")]
        public async Task<HttpResponseData> UpdateUserAddress(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "updateuseraddress")] HttpRequestData? req)
        {
      if (req == null)
          {
     throw new UserFunctionException(InvalidUserInputMessage);
            }

var inputData = req.Body ?? throw new UserFunctionException(InvalidUserInputMessage);
     var userEmailModel = await userService.GetUserEmailModel(inputData);

            var response = req.CreateResponse(HttpStatusCode.OK);

            if (await userService.DoesUserExists(userEmailModel.Email!))
 {
        var userId = await userService.UpdateUser(userEmailModel.Email!, userEmailModel.AddressId);
        logger.LogInformation("User updated with ID: {0}", userId);
     await response.WriteAsJsonAsync(userId);
 }
            else
            {
      await response.WriteAsJsonAsync("Cannot update new User as user does not exists");
            }

      return response;
  }
    }
}

