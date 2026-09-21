using Entity = Defra.PTS.User.Entities;
using Defra.PTS.User.Repositories.Interface;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using static System.Net.Mime.MediaTypeNames;
using Defra.PTS.User.Entities;

namespace Defra.PTS.User.Repositories.Implementation
{
    public class UserRepository(DbContext dbContext) : Repository<Entity.User>(dbContext), IUserRepository
    {

        private UserDbContext? UserContext
        {
            get
            {
                return _dbContext as UserDbContext;
            }
        }

        public async Task<bool> DoesUserExists(string userEmailAddress)
        {
            return await UserContext?.User.AnyAsync(a => a.Email == userEmailAddress!)!;
        }

        public async Task<Entity.User?> GetUser(string userEmailAddress)
        {
            // Ordered + FirstOrDefault (not SingleOrDefault) so historical duplicate-email rows do not throw on sign-in.
            return await UserContext?.User?
                .Where(a => a.Email == userEmailAddress)
                .OrderByDescending(a => a.CreatedOn)
                .FirstOrDefaultAsync()!;
        }

        public async Task<List<Entity.User>> GetUsersByEmailAsync(string userEmailAddress)
        {
            if (UserContext?.User == null)
                return [];

            return await UserContext.User
                .Where(a => a.Email == userEmailAddress)
                .OrderByDescending(a => a.CreatedOn)
                .ToListAsync();
        }

        [ExcludeFromCodeCoverage]
        public async Task<bool> PerformHealthCheckLogic()
        {
            try
            {
                // Attempt to open a connection to the database
                await UserContext!.Database.OpenConnectionAsync();

                // Check if the connection is open
                bool isOpen = UserContext.Database.GetDbConnection().State == ConnectionState.Open;
                
                // Close the connection to prevent connection leaks
                await UserContext.Database.CloseConnectionAsync();
                
                return isOpen;
            }
            catch
            {
                return false;
            }
        }

        public async Task<UserDetail> GetUserDetail(Guid contactId)
        {
            var user = await UserContext?.User.Where(u => u.ContactId == contactId).OrderByDescending(a => a.CreatedOn).FirstOrDefaultAsync()!;
            var address = await UserContext?.Address.Where(a => a.Id == user!.AddressId).FirstOrDefaultAsync()!;

            return new UserDetail
            {
                FullName = user?.FullName,
                Email = user?.Email,
                AddressId = address?.Id,
                Telephone = user?.Telephone,
                AddressLineOne = address?.AddressLineOne,
                AddressLineTwo = address?.AddressLineTwo,
                TownOrCity = address?.TownOrCity,
                County = address?.County,
                PostCode = address?.PostCode
            };
        }

        public async Task<Entity.User?> GetUserByContactId(Guid contactId)
        {
            if (UserContext?.User == null)
                return null;

            return await UserContext.User
                .Where(u => u.ContactId == contactId)
                .OrderByDescending(u => u.CreatedOn)
                .FirstOrDefaultAsync();
        }

        public async Task<bool> DoesUserExistsByContactId(Guid contactId)
        {
            if (UserContext?.User == null)
                return false;

            return await UserContext.User.AnyAsync(u => u.ContactId == contactId);
        }
    }
}
