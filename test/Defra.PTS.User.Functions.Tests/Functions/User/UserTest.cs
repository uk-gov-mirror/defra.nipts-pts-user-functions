using Model = Defra.PTS.User.Models;
using Defra.PTS.User.ApiServices.Interface;
using Defra.PTS.User.Models.CustomException;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;
using testFunc = Defra.PTS.User.Functions.Functions.User;
using System.Net;
using System.Text;
using Defra.PTS.User.Models;
using Entity = Defra.PTS.User.Entities;
using Defra.PTS.User.Functions.Tests.Helpers;

namespace Defra.PTS.User.Functions.Tests.Functions.User
{
    public class UserTest
    {
        private Mock<ILogger<testFunc.User>> loggerMock = new();
        private Mock<IUserService> userServiceMock = new();
    private Mock<IOwnerService> ownerServiceMock = new();
        testFunc.User? sut;

        [SetUp]
    public void SetUp()
  {
    loggerMock = new Mock<ILogger<testFunc.User>>();
            userServiceMock = new Mock<IUserService>();
     ownerServiceMock = new Mock<IOwnerService>();
            sut = new testFunc.User(userServiceMock.Object, ownerServiceMock.Object, loggerMock.Object);
        }

   [TearDown]
      public void TearDown()
        {
       loggerMock.Reset();
    userServiceMock.Reset();
    ownerServiceMock.Reset();
        }

[Test]
        public void CreateUser_WhenRequestDoesntExist_Then_ReturnsUserException()
     {
            var expectedResult = "Invalid user input, is NULL or Empty";
#pragma warning disable CS8625
     var result = Assert.ThrowsAsync<UserFunctionException>(() => sut!.CreateUser(null));
#pragma warning restore CS8625

            Assert.That(result, Is.Not.Null);
            Assert.That(result?.Message, Is.EqualTo(expectedResult));

        userServiceMock.Verify(a => a.GetUserModel(It.IsAny<Stream>()), Times.Never);
       userServiceMock.Verify(a => a.DoesUserExists(It.IsAny<string>()), Times.Never);
      userServiceMock.Verify(a => a.CreateUser(It.IsAny<Model.User>()), Times.Never);
        }

      [Test]
        public void CreateUser_WhenRequestBodyDoesntExist_Then_ReturnsUserException()
        {
            var expectedResult = "Invalid user input, is NULL or Empty";
            var requestMock = HttpRequestDataHelper.CreateMockHttpRequestData();

    var result = Assert.ThrowsAsync<UserFunctionException>(() => sut!.CreateUser(requestMock));

     Assert.That(result, Is.Not.Null);
            Assert.That(result?.Message, Is.EqualTo(expectedResult));
}

   [Test]
        public async Task CreateUser_WhenRequestBodyExists_Then_ReturnsSuccessMessageWithValidGuid()
        {
      var guid = Guid.NewGuid();
var json = JsonConvert.SerializeObject("{ \"test\" : \"success\" }");
 var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
  var requestMock = HttpRequestDataHelper.CreateMockHttpRequestData(memoryStream);

   userServiceMock.Setup(a => a.GetUserModel(It.IsAny<Stream>())).ReturnsAsync(new Model.User { Email = "test@example.com" });
            userServiceMock.Setup(a => a.DoesUserExists(It.IsAny<string>())).ReturnsAsync(false);
       userServiceMock.Setup(a => a.CreateUser(It.IsAny<Model.User>())).ReturnsAsync(guid);

  var result = await sut!.CreateUser(requestMock);

     Assert.That(result, Is.Not.Null);
   Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task CreateUser_WhenUserExists_Then_ReturnsExistingUserId()
        {
            var guid = Guid.NewGuid();
            var json = JsonConvert.SerializeObject("{ \"test\" : \"success\" }");
            var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
var requestMock = HttpRequestDataHelper.CreateMockHttpRequestData(memoryStream);

       userServiceMock.Setup(a => a.GetUserModel(It.IsAny<Stream>())).ReturnsAsync(new Model.User { Email = "test@example.com" });
        userServiceMock.Setup(a => a.DoesUserExists(It.IsAny<string>())).ReturnsAsync(true);
            userServiceMock.Setup(a => a.UpdateUser(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(guid);
            userServiceMock.Setup(a => a.GetUserIdAsync(It.IsAny<string>())).ReturnsAsync(guid);

      var result = await sut!.CreateUser(requestMock);

            Assert.That(result, Is.Not.Null);
      Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

  [Test]
     public async Task CreateUser_ContactIdExists_EmailUnchanged_ReturnsExistingUserId()
        {
     var contactId = Guid.NewGuid();
      var existingUserId = Guid.NewGuid();
       var email = "test@example.com";

   var userModel = new Model.User { ContactId = contactId, Email = email };
        var existingUser = new Entity.User { Id = existingUserId, ContactId = contactId, Email = email };

            var json = JsonConvert.SerializeObject(userModel);
            var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var requestMock = HttpRequestDataHelper.CreateMockHttpRequestData(memoryStream);

     userServiceMock.Setup(a => a.GetUserModel(It.IsAny<Stream>())).ReturnsAsync(userModel);
            userServiceMock.Setup(a => a.GetUserByContactId(contactId)).ReturnsAsync(existingUser);
            userServiceMock.Setup(a => a.UpdateUser(email, "signin")).ReturnsAsync(existingUserId);

        var result = await sut!.CreateUser(requestMock);

            Assert.That(result, Is.Not.Null);
         Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    userServiceMock.Verify(a => a.UpdateUserEmail(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    ownerServiceMock.Verify(a => a.UpdateOwnerEmailsByOldEmail(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task CreateUser_ContactIdExists_EmailChanged_UpdatesEmailsAndReturnsUserId()
        {
 var contactId = Guid.NewGuid();
            var existingUserId = Guid.NewGuid();
        var oldEmail = "old@example.com";
          var newEmail = "new@example.com";

     var userModel = new Model.User { ContactId = contactId, Email = newEmail };
            var existingUser = new Entity.User { Id = existingUserId, ContactId = contactId, Email = oldEmail };

 var json = JsonConvert.SerializeObject(userModel);
        var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var requestMock = HttpRequestDataHelper.CreateMockHttpRequestData(memoryStream);

    userServiceMock.Setup(a => a.GetUserModel(It.IsAny<Stream>())).ReturnsAsync(userModel);
            userServiceMock.Setup(a => a.GetUserByContactId(contactId)).ReturnsAsync(existingUser);
     userServiceMock.Setup(a => a.UpdateUserEmail(oldEmail, newEmail)).Returns(Task.CompletedTask);
            ownerServiceMock.Setup(a => a.UpdateOwnerEmailsByOldEmail(oldEmail, newEmail)).Returns(Task.CompletedTask);
            userServiceMock.Setup(a => a.UpdateUser(newEmail, "signin")).ReturnsAsync(existingUserId);

            var result = await sut!.CreateUser(requestMock);

  Assert.That(result, Is.Not.Null);
       Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    userServiceMock.Verify(a => a.UpdateUserEmail(oldEmail, newEmail), Times.Once);
  ownerServiceMock.Verify(a => a.UpdateOwnerEmailsByOldEmail(oldEmail, newEmail), Times.Once);
    }

        [Test]
        public async Task CreateUser_ContactIdNotFound_CreatesNewUser()
        {
  var contactId = Guid.NewGuid();
      var newUserId = Guid.NewGuid();
            var email = "new@example.com";

  var userModel = new Model.User { ContactId = contactId, Email = email };

       var json = JsonConvert.SerializeObject(userModel);
            var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
     var requestMock = HttpRequestDataHelper.CreateMockHttpRequestData(memoryStream);

  userServiceMock.Setup(a => a.GetUserModel(It.IsAny<Stream>())).ReturnsAsync(userModel);
            userServiceMock.Setup(a => a.GetUserByContactId(contactId)).ReturnsAsync((Entity.User?)null);
            userServiceMock.Setup(a => a.DoesUserExists(email)).ReturnsAsync(false);
         userServiceMock.Setup(a => a.CreateUser(userModel)).ReturnsAsync(newUserId);

    var result = await sut!.CreateUser(requestMock);

          Assert.That(result, Is.Not.Null);
            Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            userServiceMock.Verify(a => a.CreateUser(userModel), Times.Once);
        }

        [Test]
        public async Task CreateUser_ContactIdNotFound_SameIdentityEmailHolder_ReturnsExistingUserId()
   {
         var contactId = Guid.NewGuid();
 var existingUserId = Guid.NewGuid();
     var email = "existing@example.com";

        var userModel = new Model.User { ContactId = contactId, Uniquereference = "REF1", Email = email };
        var existingUser = new Entity.User { Id = existingUserId, Uniquereference = "REF1", Email = email };

 var json = JsonConvert.SerializeObject(userModel);
   var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var requestMock = HttpRequestDataHelper.CreateMockHttpRequestData(memoryStream);

            userServiceMock.Setup(a => a.GetUserModel(It.IsAny<Stream>())).ReturnsAsync(userModel);
userServiceMock.Setup(a => a.GetUserByContactId(contactId)).ReturnsAsync((Entity.User?)null);
 userServiceMock.Setup(a => a.GetUsersByEmail(email)).ReturnsAsync([existingUser]);
       userServiceMock.Setup(a => a.UpdateUser(email, "signin")).ReturnsAsync(existingUserId);

   var result = await sut!.CreateUser(requestMock);

 Assert.That(result, Is.Not.Null);
  Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            userServiceMock.Verify(a => a.UpdateUser(email, "signin"), Times.Once);
      userServiceMock.Verify(a => a.CreateUser(It.IsAny<Model.User>()), Times.Never);
      userServiceMock.Verify(a => a.RetireUserEmail(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task CreateUser_ReRegistrationWithNewUniqueReference_RetiresOldAndCreatesNew()
        {
            var newContactId = Guid.NewGuid();
            var oldContactId = Guid.NewGuid();
            var oldUserId = Guid.NewGuid();
            var newUserId = Guid.NewGuid();
            var email = "contact@help.com";

            var userModel = new Model.User { ContactId = newContactId, Uniquereference = "REF-NEW", Email = email };
            var oldUser = new Entity.User { Id = oldUserId, Uniquereference = "REF-OLD", ContactId = oldContactId, Email = email };

            var json = JsonConvert.SerializeObject(userModel);
            var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var requestMock = HttpRequestDataHelper.CreateMockHttpRequestData(memoryStream);

            userServiceMock.Setup(a => a.GetUserModel(It.IsAny<Stream>())).ReturnsAsync(userModel);
            userServiceMock.Setup(a => a.GetUserByContactId(newContactId)).ReturnsAsync((Entity.User?)null);
            userServiceMock.Setup(a => a.GetUsersByEmail(email)).ReturnsAsync([oldUser]);
            userServiceMock.Setup(a => a.RetireUserEmail(It.IsAny<Guid>(), It.IsAny<string>())).Returns(Task.CompletedTask);
            ownerServiceMock.Setup(a => a.UpdateOwnerEmailsByOldEmail(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
            userServiceMock.Setup(a => a.CreateUser(userModel)).ReturnsAsync(newUserId);

            var result = await sut!.CreateUser(requestMock);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var expectedRetiredEmail = $"{email}.REFOLD.{oldContactId:N}.invalid";
            userServiceMock.Verify(a => a.RetireUserEmail(oldUserId, expectedRetiredEmail), Times.Once);
            ownerServiceMock.Verify(a => a.UpdateOwnerEmailsByOldEmail(email, expectedRetiredEmail), Times.Once);
            userServiceMock.Verify(a => a.CreateUser(userModel), Times.Once);
            userServiceMock.Verify(a => a.UpdateUser(It.IsAny<string>(), "signin"), Times.Never);
        }

        [Test]
        public async Task CreateUser_ContactIdChanged_SameUniqueReference_ReusesExistingUserWithoutSplitting()
        {
            var newContactId = Guid.NewGuid();
            var oldContactId = Guid.NewGuid();
            var existingUserId = Guid.NewGuid();
            var email = "person@example.com";

            var userModel = new Model.User { ContactId = newContactId, Uniquereference = "REF1", Email = email };
            var existingUser = new Entity.User { Id = existingUserId, ContactId = oldContactId, Uniquereference = "REF1", Email = email };

            var json = JsonConvert.SerializeObject(userModel);
            var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var requestMock = HttpRequestDataHelper.CreateMockHttpRequestData(memoryStream);

            userServiceMock.Setup(a => a.GetUserModel(It.IsAny<Stream>())).ReturnsAsync(userModel);
            // IDM re-issued the ContactId, so the row is not found by ContactId.
            userServiceMock.Setup(a => a.GetUserByContactId(newContactId)).ReturnsAsync((Entity.User?)null);
            userServiceMock.Setup(a => a.GetUsersByEmail(email)).ReturnsAsync([existingUser]);
            userServiceMock.Setup(a => a.UpdateUser(email, "signin")).ReturnsAsync(existingUserId);

            var result = await sut!.CreateUser(requestMock);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            userServiceMock.Verify(a => a.UpdateUser(email, "signin"), Times.Once);
            userServiceMock.Verify(a => a.RetireUserEmail(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
            userServiceMock.Verify(a => a.CreateUser(It.IsAny<Model.User>()), Times.Never);
        }

        [Test]
        public async Task CreateUser_ContactIdChanged_NoUniqueReference_ReusesExistingUserWithoutSplitting()
        {
            var newContactId = Guid.NewGuid();
            var oldContactId = Guid.NewGuid();
            var existingUserId = Guid.NewGuid();
            var email = "person@example.com";

            // Neither side carries a Uniquereference, only the ContactId differs -> must NOT be treated as a new identity.
            var userModel = new Model.User { ContactId = newContactId, Email = email };
            var existingUser = new Entity.User { Id = existingUserId, ContactId = oldContactId, Email = email };

            var json = JsonConvert.SerializeObject(userModel);
            var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var requestMock = HttpRequestDataHelper.CreateMockHttpRequestData(memoryStream);

            userServiceMock.Setup(a => a.GetUserModel(It.IsAny<Stream>())).ReturnsAsync(userModel);
            userServiceMock.Setup(a => a.GetUserByContactId(newContactId)).ReturnsAsync((Entity.User?)null);
            userServiceMock.Setup(a => a.GetUsersByEmail(email)).ReturnsAsync([existingUser]);
            userServiceMock.Setup(a => a.UpdateUser(email, "signin")).ReturnsAsync(existingUserId);

            var result = await sut!.CreateUser(requestMock);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            userServiceMock.Verify(a => a.RetireUserEmail(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
            userServiceMock.Verify(a => a.CreateUser(It.IsAny<Model.User>()), Times.Never);
        }

        [Test]
        public async Task CreateUser_EmailExistsWithNoComparableIdentity_ReusesExistingUser()
        {
            var existingUserId = Guid.NewGuid();
            var email = "legacy@example.com";

            // Caller supplies neither ContactId nor Uniquereference -> legacy email-only behaviour.
            var userModel = new Model.User { Email = email };
            var existingUser = new Entity.User { Id = existingUserId, Email = email };

            var json = JsonConvert.SerializeObject(userModel);
            var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var requestMock = HttpRequestDataHelper.CreateMockHttpRequestData(memoryStream);

            userServiceMock.Setup(a => a.GetUserModel(It.IsAny<Stream>())).ReturnsAsync(userModel);
            userServiceMock.Setup(a => a.GetUsersByEmail(email)).ReturnsAsync([existingUser]);
            userServiceMock.Setup(a => a.UpdateUser(email, "signin")).ReturnsAsync(existingUserId);

            var result = await sut!.CreateUser(requestMock);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            userServiceMock.Verify(a => a.UpdateUser(email, "signin"), Times.Once);
            userServiceMock.Verify(a => a.CreateUser(It.IsAny<Model.User>()), Times.Never);
            userServiceMock.Verify(a => a.RetireUserEmail(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
        }


        [Test]
 public async Task CreateUser_NoContactId_FallsBackToEmailLogic()
        {
            var email = "test@example.com";
     var newUserId = Guid.NewGuid();

      var userModel = new Model.User { ContactId = null, Email = email };

            var json = JsonConvert.SerializeObject(userModel);
          var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
     var requestMock = HttpRequestDataHelper.CreateMockHttpRequestData(memoryStream);

    userServiceMock.Setup(a => a.GetUserModel(It.IsAny<Stream>())).ReturnsAsync(userModel);
        userServiceMock.Setup(a => a.DoesUserExists(email)).ReturnsAsync(false);
userServiceMock.Setup(a => a.CreateUser(userModel)).ReturnsAsync(newUserId);

            var result = await sut!.CreateUser(requestMock);

   Assert.That(result, Is.Not.Null);
     Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));

userServiceMock.Verify(a => a.GetUserByContactId(It.IsAny<Guid>()), Times.Never);
        userServiceMock.Verify(a => a.GetUsersByEmail(email), Times.Once);
        }

    [Test]
        public async Task CreateUser_EmptyContactId_FallsBackToEmailLogic()
        {
    var email = "test@example.com";
        var newUserId = Guid.NewGuid();

         var userModel = new Model.User { ContactId = Guid.Empty, Email = email };

            var json = JsonConvert.SerializeObject(userModel);
        var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
      var requestMock = HttpRequestDataHelper.CreateMockHttpRequestData(memoryStream);

      userServiceMock.Setup(a => a.GetUserModel(It.IsAny<Stream>())).ReturnsAsync(userModel);
            userServiceMock.Setup(a => a.DoesUserExists(email)).ReturnsAsync(false);
            userServiceMock.Setup(a => a.CreateUser(userModel)).ReturnsAsync(newUserId);

 var result = await sut!.CreateUser(requestMock);

    Assert.That(result, Is.Not.Null);
   Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    userServiceMock.Verify(a => a.GetUserByContactId(It.IsAny<Guid>()), Times.Never);
     }

        [Test]
    public async Task CreateUser_EmailUpdateThrowsException_ContinuesExecution()
     {
            var contactId = Guid.NewGuid();
            var existingUserId = Guid.NewGuid();
     var oldEmail = "old@example.com";
 var newEmail = "new@example.com";

 var userModel = new Model.User { ContactId = contactId, Email = newEmail };
    var existingUser = new Entity.User { Id = existingUserId, ContactId = contactId, Email = oldEmail };

         var json = JsonConvert.SerializeObject(userModel);
   var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var requestMock = HttpRequestDataHelper.CreateMockHttpRequestData(memoryStream);

     userServiceMock.Setup(a => a.GetUserModel(It.IsAny<Stream>())).ReturnsAsync(userModel);
            userServiceMock.Setup(a => a.GetUserByContactId(contactId)).ReturnsAsync(existingUser);
         userServiceMock.Setup(a => a.UpdateUserEmail(oldEmail, newEmail)).ThrowsAsync(new Exception("Database error"));
    userServiceMock.Setup(a => a.UpdateUser(newEmail, "signin")).ReturnsAsync(existingUserId);

     var result = await sut!.CreateUser(requestMock);

            Assert.That(result, Is.Not.Null);
     Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task CreateUser_SignInUpdateThrowsException_ContinuesExecution()
  {
        var contactId = Guid.NewGuid();
       var existingUserId = Guid.NewGuid();
            var email = "test@example.com";

    var userModel = new Model.User { ContactId = contactId, Email = email };
   var existingUser = new Entity.User { Id = existingUserId, ContactId = contactId, Email = email };

   var json = JsonConvert.SerializeObject(userModel);
         var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
 var requestMock = HttpRequestDataHelper.CreateMockHttpRequestData(memoryStream);

            userServiceMock.Setup(a => a.GetUserModel(It.IsAny<Stream>())).ReturnsAsync(userModel);
     userServiceMock.Setup(a => a.GetUserByContactId(contactId)).ReturnsAsync(existingUser);
            userServiceMock.Setup(a => a.UpdateUser(email, "signin")).ThrowsAsync(new Exception("Sign-in update failed"));

      var result = await sut!.CreateUser(requestMock);

      Assert.That(result, Is.Not.Null);
  Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

 [Test]
  public void CreateUser_WhenUserModelIsNull_ThrowsException()
     {
     var json = JsonConvert.SerializeObject("{ \"test\" : \"success\" }");
       var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
var requestMock = HttpRequestDataHelper.CreateMockHttpRequestData(memoryStream);
#pragma warning disable CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types.
   userServiceMock.Setup(a => a.GetUserModel(It.IsAny<Stream>())).ReturnsAsync((Model.User?)null);
#pragma warning restore CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types.

    var result = Assert.ThrowsAsync<UserFunctionException>(() => sut!.CreateUser(requestMock));
  Assert.That(result!.Message, Is.EqualTo("Failed to parse user model from input data"));
        }

        [Test]
        public void CreateUser_WhenNoContactIdAndNoEmail_ThrowsException()
        {
            var userModel = new Model.User { ContactId = null, Email = "" };

            var json = JsonConvert.SerializeObject(userModel);
var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var requestMock = HttpRequestDataHelper.CreateMockHttpRequestData(memoryStream);

            userServiceMock.Setup(a => a.GetUserModel(It.IsAny<Stream>())).ReturnsAsync(userModel);

   var result = Assert.ThrowsAsync<UserFunctionException>(() => sut!.CreateUser(requestMock));
        Assert.That(result!.Message, Is.EqualTo("User model must have either ContactId or Email"));
        }

  [Test]
   public async Task CreateUser_EmailsAreEmpty_SkipsEmailUpdate()
{
            var contactId = Guid.NewGuid();
    var existingUserId = Guid.NewGuid();

     var userModel = new Model.User { ContactId = contactId, Email = "" };
            var existingUser = new Entity.User { Id = existingUserId, ContactId = contactId, Email = "" };

  var json = JsonConvert.SerializeObject(userModel);
            var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var requestMock = HttpRequestDataHelper.CreateMockHttpRequestData(memoryStream);

       userServiceMock.Setup(a => a.GetUserModel(It.IsAny<Stream>())).ReturnsAsync(userModel);
       userServiceMock.Setup(a => a.GetUserByContactId(contactId)).ReturnsAsync(existingUser);

    var result = await sut!.CreateUser(requestMock);

 Assert.That(result, Is.Not.Null);
          Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            userServiceMock.Verify(a => a.UpdateUserEmail(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
       ownerServiceMock.Verify(a => a.UpdateOwnerEmailsByOldEmail(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

     [Test]
   public async Task CreateUser_OnlyOldEmailEmpty_SkipsEmailUpdate()
  {
            var contactId = Guid.NewGuid();
 var existingUserId = Guid.NewGuid();

            var userModel = new Model.User { ContactId = contactId, Email = "new@example.com" };
            var existingUser = new Entity.User { Id = existingUserId, ContactId = contactId, Email = "" };

            var json = JsonConvert.SerializeObject(userModel);
            var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var requestMock = HttpRequestDataHelper.CreateMockHttpRequestData(memoryStream);

         userServiceMock.Setup(a => a.GetUserModel(It.IsAny<Stream>())).ReturnsAsync(userModel);
            userServiceMock.Setup(a => a.GetUserByContactId(contactId)).ReturnsAsync(existingUser);
            userServiceMock.Setup(a => a.UpdateUser("new@example.com", "signin")).ReturnsAsync(existingUserId);

            var result = await sut!.CreateUser(requestMock);

    Assert.That(result, Is.Not.Null);
       Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));

          userServiceMock.Verify(a => a.UpdateUserEmail(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

  [Test]
   public async Task CreateUser_OnlyNewEmailEmpty_SkipsEmailUpdate()
        {
   var contactId = Guid.NewGuid();
            var existingUserId = Guid.NewGuid();

            var userModel = new Model.User { ContactId = contactId, Email = "" };
   var existingUser = new Entity.User { Id = existingUserId, ContactId = contactId, Email = "old@example.com" };

       var json = JsonConvert.SerializeObject(userModel);
        var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var requestMock = HttpRequestDataHelper.CreateMockHttpRequestData(memoryStream);

            userServiceMock.Setup(a => a.GetUserModel(It.IsAny<Stream>())).ReturnsAsync(userModel);
    userServiceMock.Setup(a => a.GetUserByContactId(contactId)).ReturnsAsync(existingUser);

     var result = await sut!.CreateUser(requestMock);

      Assert.That(result, Is.Not.Null);
            Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            userServiceMock.Verify(a => a.UpdateUserEmail(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            userServiceMock.Verify(a => a.UpdateUser(It.IsAny<string>(), "signin"), Times.Never);
     }

     [Test]
        public async Task UpdateUser_WhenValidData_ReturnsGuid()
        {
   var guid = Guid.NewGuid();
     var json = JsonConvert.SerializeObject("{ \"Email\" : \"test@example.com\" }");
    var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var requestMock = HttpRequestDataHelper.CreateMockHttpRequestData(memoryStream);

            userServiceMock.Setup(a => a.GetUserEmailModel(It.IsAny<Stream>())).ReturnsAsync(new UserEmail { Email = "test@example.com", Type = "signin" });
       userServiceMock.Setup(a => a.DoesUserExists(It.IsAny<string>())).ReturnsAsync(true);
         userServiceMock.Setup(a => a.UpdateUser(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(guid);

 var result = await sut!.UpdateUser(requestMock);

      Assert.That(result, Is.Not.Null);
            Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));
   }

    [Test]
        public async Task UpdateUser_WhenUserDoesNotExist_ReturnsErrorMessage()
        {
    var json = JsonConvert.SerializeObject("{ \"Email\" : \"test@example.com\" }");
     var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
          var requestMock = HttpRequestDataHelper.CreateMockHttpRequestData(memoryStream);

       userServiceMock.Setup(a => a.GetUserEmailModel(It.IsAny<Stream>())).ReturnsAsync(new UserEmail { Email = "test@example.com", Type = "signin" });
            userServiceMock.Setup(a => a.DoesUserExists(It.IsAny<string>())).ReturnsAsync(false);

        var result = await sut!.UpdateUser(requestMock);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public void UpdateUser_WhenRequestIsNull_ThrowsException()
        {
#pragma warning disable CS8625
  var result = Assert.ThrowsAsync<UserFunctionException>(() => sut!.UpdateUser(null));
#pragma warning restore CS8625
            Assert.That(result!.Message, Is.EqualTo("Invalid user input, is NUll or Empty"));
  }

   [Test]
        public void UpdateUser_WhenRequestBodyIsNull_ThrowsException()
    {
         var requestMock = HttpRequestDataHelper.CreateMockHttpRequestData();
       var result = Assert.ThrowsAsync<UserFunctionException>(() => sut!.UpdateUser(requestMock));
     Assert.That(result!.Message, Is.EqualTo("Invalid user input, is NUll or Empty"));
        }

      [Test]
     public async Task UpdateUserAddress_WhenValidData_ReturnsGuid()
  {
 var userId = Guid.NewGuid();
  var json = JsonConvert.SerializeObject("{ \"Email\" : \"test@example.com\" }");
 var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var requestMock = HttpRequestDataHelper.CreateMockHttpRequestData(memoryStream);

            userServiceMock.Setup(a => a.GetUserEmailModel(It.IsAny<Stream>())).ReturnsAsync(new UserEmail { Email = "test@example.com", Type = "signin" });
            userServiceMock.Setup(a => a.DoesUserExists(It.IsAny<string>())).ReturnsAsync(true);
      userServiceMock.Setup(a => a.UpdateUser(It.IsAny<string>(), It.IsAny<Guid?>())).ReturnsAsync(userId);

  var result = await sut!.UpdateUserAddress(requestMock);

         Assert.That(result, Is.Not.Null);
          Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

      [Test]
        public async Task UpdateUserAddress_WhenUserDoesNotExist_ReturnsErrorMessage()
        {
          var json = JsonConvert.SerializeObject("{ \"Email\" : \"test@example.com\" }");
var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
    var requestMock = HttpRequestDataHelper.CreateMockHttpRequestData(memoryStream);

    userServiceMock.Setup(a => a.GetUserEmailModel(It.IsAny<Stream>())).ReturnsAsync(new UserEmail { Email = "test@example.com", Type = "signin" });
   userServiceMock.Setup(a => a.DoesUserExists(It.IsAny<string>())).ReturnsAsync(false);

            var result = await sut!.UpdateUserAddress(requestMock);

      Assert.That(result, Is.Not.Null);
          Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));
   }

  [Test]
        public void UpdateUserAddress_WhenRequestIsNull_ThrowsException()
    {
#pragma warning disable CS8625
         var result = Assert.ThrowsAsync<UserFunctionException>(() => sut!.UpdateUserAddress(null));
#pragma warning restore CS8625
            Assert.That(result!.Message, Is.EqualTo("Invalid user input, is NUll or Empty"));
        }

        [Test]
 public void UpdateUserAddress_WhenRequestBodyIsNull_ThrowsException()
     {
      var requestMock = HttpRequestDataHelper.CreateMockHttpRequestData();
     var result = Assert.ThrowsAsync<UserFunctionException>(() => sut!.UpdateUserAddress(requestMock));
          Assert.That(result!.Message, Is.EqualTo("Invalid user input, is NUll or Empty"));
   }

  [Test]
  public async Task CreateUser_ContactIdExists_EmailNull_SkipsEmailAndSignInUpdate()
     {
    var contactId = Guid.NewGuid();
            var existingUserId = Guid.NewGuid();

    var userModel = new Model.User { ContactId = contactId, Email = null };
            var existingUser = new Entity.User { Id = existingUserId, ContactId = contactId, Email = "existing@example.com" };

     var json = JsonConvert.SerializeObject(userModel);
    var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var requestMock = HttpRequestDataHelper.CreateMockHttpRequestData(memoryStream);

 userServiceMock.Setup(a => a.GetUserModel(It.IsAny<Stream>())).ReturnsAsync(userModel);
   userServiceMock.Setup(a => a.GetUserByContactId(contactId)).ReturnsAsync(existingUser);

   var result = await sut!.CreateUser(requestMock);

          Assert.That(result, Is.Not.Null);
          Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));

   userServiceMock.Verify(a => a.UpdateUser(It.IsAny<string>(), "signin"), Times.Never);
 }

      [Test]
   public async Task CreateUser_ExistingUserEmailNull_SkipsEmailUpdate()
        {
         var contactId = Guid.NewGuid();
       var existingUserId = Guid.NewGuid();

     var userModel = new Model.User { ContactId = contactId, Email = "new@example.com" };
     var existingUser = new Entity.User { Id = existingUserId, ContactId = contactId, Email = null };

          var json = JsonConvert.SerializeObject(userModel);
    var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
var requestMock = HttpRequestDataHelper.CreateMockHttpRequestData(memoryStream);

            userServiceMock.Setup(a => a.GetUserModel(It.IsAny<Stream>())).ReturnsAsync(userModel);
  userServiceMock.Setup(a => a.GetUserByContactId(contactId)).ReturnsAsync(existingUser);
        userServiceMock.Setup(a => a.UpdateUser("new@example.com", "signin")).ReturnsAsync(existingUserId);

            var result = await sut!.CreateUser(requestMock);

            Assert.That(result, Is.Not.Null);
        Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));

   userServiceMock.Verify(a => a.UpdateUserEmail(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            userServiceMock.Verify(a => a.UpdateUser("new@example.com", "signin"), Times.Once);
     }

 [Test]
        public async Task CreateUser_OwnerUpdateFails_StillSucceeds()
        {
         var contactId = Guid.NewGuid();
            var existingUserId = Guid.NewGuid();
       var oldEmail = "old@example.com";
      var newEmail = "new@example.com";

            var userModel = new Model.User { ContactId = contactId, Email = newEmail };
       var existingUser = new Entity.User { Id = existingUserId, ContactId = contactId, Email = oldEmail };

       var json = JsonConvert.SerializeObject(userModel);
            var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
    var requestMock = HttpRequestDataHelper.CreateMockHttpRequestData(memoryStream);

         userServiceMock.Setup(a => a.GetUserModel(It.IsAny<Stream>())).ReturnsAsync(userModel);
            userServiceMock.Setup(a => a.GetUserByContactId(contactId)).ReturnsAsync(existingUser);
     userServiceMock.Setup(a => a.UpdateUserEmail(oldEmail, newEmail)).Returns(Task.CompletedTask);
      ownerServiceMock.Setup(a => a.UpdateOwnerEmailsByOldEmail(oldEmail, newEmail)).ThrowsAsync(new Exception("Owner update failed"));
            userServiceMock.Setup(a => a.UpdateUser(newEmail, "signin")).ReturnsAsync(existingUserId);

    var result = await sut!.CreateUser(requestMock);

   Assert.That(result, Is.Not.Null);
      Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
   public async Task CreateUser_EmailUpdateFailsWithSignInSucceeds_ReturnsSuccess()
        {
      var email = "test@example.com";
        var userId = Guid.NewGuid();

      var userModel = new Model.User { ContactId = null, Email = email };

     var json = JsonConvert.SerializeObject(userModel);
   var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
       var requestMock = HttpRequestDataHelper.CreateMockHttpRequestData(memoryStream);

          userServiceMock.Setup(a => a.GetUserModel(It.IsAny<Stream>())).ReturnsAsync(userModel);
 userServiceMock.Setup(a => a.DoesUserExists(email)).ReturnsAsync(true);
            userServiceMock.Setup(a => a.UpdateUser(email, "signin")).ThrowsAsync(new Exception("Update failed"));
            userServiceMock.Setup(a => a.GetUserIdAsync(email)).ReturnsAsync(userId);

         var result = await sut!.CreateUser(requestMock);

     Assert.That(result, Is.Not.Null);
            Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }
    }
}