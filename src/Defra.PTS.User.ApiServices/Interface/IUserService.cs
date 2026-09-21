using Defra.PTS.User.Entities;
using Model = Defra.PTS.User.Models;
using Entity = Defra.PTS.User.Entities;

namespace Defra.PTS.User.ApiServices.Interface
{
    /// <summary>
    /// IUserService
    /// </summary>
    public interface IUserService
    {
        Task<Model.User> GetUserModel(Stream userStream);
        Task<bool> DoesUserExists(string userEmail);
        Task<Guid> CreateUser(Model.User userModel);
        Task<Guid> GetUserIdAsync(string userEmail);
        Task<Guid> UpdateUser(string userEmail, string type);
        Task<Guid> UpdateUser(string userEmail, Guid? addressId);
        Task<Model.UserEmail> GetUserEmailModel(Stream userStream);
        Task<bool> PerformHealthCheckLogic();
        Task<UserDetail> GetUserDetail(Guid contactId);
        Task<Entity.User?> GetUserByContactId(Guid contactId);
        Task<bool> DoesUserExistsByContactId(Guid contactId);
        Task UpdateUserEmail(string oldEmail, string newEmail);
        Task<List<Entity.User>> GetUsersByEmail(string userEmail);
        Task RetireUserEmail(Guid userId, string newEmail);
        Task<Model.OwnerEmailUpdateModel> GetOwnerEmailUpdateModel(Stream inputStream);
    }
}   
