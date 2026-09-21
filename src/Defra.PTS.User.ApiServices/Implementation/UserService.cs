using Defra.PTS.User.ApiServices.Interface;
using Defra.PTS.User.Repositories.Interface;
using Entity = Defra.PTS.User.Entities;
using Model = Defra.PTS.User.Models;
using System.Text.Json;
using Defra.PTS.User.Models.CustomException;
using Defra.PTS.User.Entities;
using System.Diagnostics.CodeAnalysis;

namespace Defra.PTS.User.ApiServices.Implementation
{    
    public class UserService(IUserRepository userRepository) : IUserService
    {        
        private readonly IUserRepository _userRepository = userRepository;
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public async Task<Guid> CreateUser(Model.User userModel)
        {
            var userDB = new Entity.User()
            {
                Email = userModel.Email,
                FullName = userModel.FullName,
                FirstName = userModel.FirstName!,
                LastName = userModel.LastName!,
                Role = userModel.Role!,
                Telephone = userModel.Telephone,
                ContactId = userModel.ContactId,
                Uniquereference = userModel.Uniquereference,
                SignInDateTime = userModel.SignInDateTime,
                SignOutDateTime = userModel.SignInDateTime,
                CreatedBy = userModel.CreatedBy,
                CreatedOn = DateTime.Now
            };

            await _userRepository.Add(userDB);
            await _userRepository.SaveChanges();

            return userDB.Id;
        }

        public async Task<Guid> GetUserIdAsync(string userEmail)
        {
            var user = await _userRepository.GetUser(userEmail);
            return user!.Id;
        }

        public async Task<Guid> UpdateUser(string userEmail, string type)
        {

            var userDB = _userRepository.GetUser(userEmail).Result;
            if (type == "signin")
                userDB!.SignInDateTime = DateTime.Now;
            else
                userDB!.SignOutDateTime = DateTime.Now;

            _userRepository.Update(userDB);
            await _userRepository.SaveChanges();

            return userDB.Id;

        }

        public async Task<Guid> UpdateUser(string userEmail, Guid? addressId)
        {

            var userDB = _userRepository.GetUser(userEmail).Result;
            userDB!.AddressId = addressId;
            userDB.UpdatedOn = DateTime.Now;

            _userRepository.Update(userDB);
            await _userRepository.SaveChanges();

            return userDB.Id;

        }

        public async Task<bool> DoesUserExists(string userEmail)
        {
            if (string.IsNullOrEmpty(userEmail))
            {
                throw new UserFunctionException("Invalid User Email Address");
            }

            return await _userRepository.DoesUserExists(userEmail);
        }

        public async Task<Model.User> GetUserModel(Stream userStream)
        {
            try
            {
                string user = await new StreamReader(userStream).ReadToEndAsync();
                Model.User? userModel = JsonSerializer.Deserialize<Model.User>(user, _jsonOptions);

                return userModel!;
            }
            catch
            {
                throw new UserFunctionException("Cannot create User as User Model Cannot be Deserialized");
            }
        }

        public async Task<Model.UserEmail> GetUserEmailModel(Stream userStream)
        {
            try
            {
                string userEmail = await new StreamReader(userStream).ReadToEndAsync();
                Model.UserEmail? userEmailModel = JsonSerializer.Deserialize<Model.UserEmail>(userEmail, _jsonOptions);
   
                return userEmailModel!;
            }
            catch
            {
                throw new UserFunctionException("Cannot create User as UserEmail Model Cannot be Deserialized");
            }        
        }

        [ExcludeFromCodeCoverage]
        public async Task<bool> PerformHealthCheckLogic()
        {
            return await _userRepository.PerformHealthCheckLogic();
        }

        public async Task<UserDetail> GetUserDetail(Guid contactId)
        {
            return await _userRepository.GetUserDetail(contactId);
        }

        public async Task<Entity.User?> GetUserByContactId(Guid contactId)
        {
            return await _userRepository.GetUserByContactId(contactId);
        }

        public async Task<bool> DoesUserExistsByContactId(Guid contactId)
        {
            return await _userRepository.DoesUserExistsByContactId(contactId);
        }

        public async Task UpdateUserEmail(string oldEmail, string newEmail)
        {
            if (string.IsNullOrEmpty(oldEmail) || string.IsNullOrEmpty(newEmail))
                return;

            var user = await _userRepository.GetUser(oldEmail);
            if (user != null)
            {
                user.Email = newEmail;
                user.UpdatedOn = DateTime.UtcNow;
                _userRepository.Update(user);
                await _userRepository.SaveChanges();
            }
        }

        public async Task<List<Entity.User>> GetUsersByEmail(string userEmail)
        {
            if (string.IsNullOrEmpty(userEmail))
                return [];

            return await _userRepository.GetUsersByEmailAsync(userEmail);
        }

        public async Task RetireUserEmail(Guid userId, string newEmail)
        {
            if (userId == Guid.Empty || string.IsNullOrEmpty(newEmail))
                return;

            var user = _userRepository.Find(userId);
            if (user == null)
                return;

            user.Email = newEmail;
            user.UpdatedOn = DateTime.UtcNow;
            _userRepository.Update(user);
            await _userRepository.SaveChanges();
        }

        public async Task<Model.OwnerEmailUpdateModel> GetOwnerEmailUpdateModel(Stream inputStream)
        {
            try
            {
                string content = await new StreamReader(inputStream).ReadToEndAsync();
                Model.OwnerEmailUpdateModel? model = JsonSerializer.Deserialize<Model.OwnerEmailUpdateModel>(content, _jsonOptions);
                return model!;
            }
            catch
            {
                throw new UserFunctionException("Cannot deserialize OwnerEmailUpdateModel");
            }
        }

    }
}
