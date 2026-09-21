using Entity = Defra.PTS.User.Entities;
using Defra.PTS.User.Entities;

namespace Defra.PTS.User.Repositories.Interface
{
    /// <summary>
    /// IUserRepository
    /// </summary>
    public interface IUserRepository : IRepository<Entity.User>
    {
        Task<bool> DoesUserExists(string userEmailAddress);
        Task<Entity.User?> GetUser(string userEmailAddress);
        Task<List<Entity.User>> GetUsersByEmailAsync(string userEmailAddress);
        Task<bool> PerformHealthCheckLogic();
        Task<UserDetail> GetUserDetail(Guid contactId);
        Task<Entity.User?> GetUserByContactId(Guid contactId);
        Task<bool> DoesUserExistsByContactId(Guid contactId);
    }
}
