using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DatingApp.API.Helpers;
using DatingApp.API.Models;
using Microsoft.EntityFrameworkCore;

namespace DatingApp.API.Data
{
    public class DatingRepository : IDatingRepository
    {
        private readonly DataContext _context;
        public DatingRepository(DataContext context)
        {
            _context = context;
        }

        public void Add<T>(T entity) where T : class
        {
            _context.Add(entity);
        }

        public void Delete<T>(T entity) where T : class
        {
            _context.Remove(entity);
        }

        public async Task<User> GetUser(int userId)
        {
            return await _context.Users
                .Include(p => p.Photos)
                .FirstOrDefaultAsync(u => u.Id == userId);
        }

        public async Task<PagedList<User>> GetUsers(UserParams userParams)
        {
            var query =  _context.Users.Include(p => p.Photos).OrderByDescending(u=> u.LastActive).AsQueryable();
            
            // filter out current user from list, no point showing yourself in the list
            query = query.Where(u=> u.Id != userParams.UserId);

            query = query.Where(u=> u.Gender == userParams.Gender);

            if(userParams.Likers)
            {
                var userLikers = await GetUserLikes(userParams.UserId, userParams.Likers);
                query = query.Where(u=> userLikers.Contains(u.Id));
            }

            if(userParams.Likees)
            {
                var userLikees = await GetUserLikes(userParams.UserId, userParams.Likers);
                query = query.Where(u => userLikees.Contains(u.Id));
            }

            if(userParams.MinAge != 18 || userParams.MaxAge != 99)
            {
                var minDob = DateTime.Today.AddYears(-userParams.MaxAge - 1);
                var maxDob = DateTime.Today.AddYears(-userParams.MinAge);

                query = query.Where(u=> u.DateOfBirth >= minDob && u.DateOfBirth <= maxDob);
            }

            if(!string.IsNullOrEmpty(userParams.OrderBy))
            {
                switch(userParams.OrderBy)
                {
                    case "created":
                        query = query.OrderByDescending(u=> u.Created);
                        break;
                    default:
                        query = query.OrderByDescending(u=> u.LastActive);
                        break;
                }
            }

            return await PagedList<User>.CreateAsync(query, userParams.PageNumber, userParams.PageSize);
        }

        private async Task<IEnumerable<int>> GetUserLikes(int id, bool likers)
        {
            var user = await _context.Users
                .Include(x=> x.Likers)
                .Include(x=>x.Likees)
                .FirstOrDefaultAsync(u=> u.Id == id);
            
            if(likers)
            {
                return user.Likers.Where(u=> u.LikeeId == id).Select(u=> u.LikerId);
            }
            else
            {
                return user.Likees.Where(u => u.LikerId == id).Select(u => u.LikeeId);
            }
        }

        public async Task<bool> SaveAll()
        {
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<Photo> GetPhoto(int id)
        {
            return await _context.Photos.FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<Photo> GetMainPhotoForUser(int userId)
        {
            return await _context.Photos.Where(u=> u.UserId == userId).FirstOrDefaultAsync(p=> p.IsMain);
        }

        public async Task<Like> GetLike(int userId, int recipientId)
        {
            return await _context.Likes.FirstOrDefaultAsync(u=> u.LikeeId == userId && u.LikeeId == recipientId);
        }

        public async Task<Message> GetMessage(int id)
        {
           return await _context.Messages.FirstOrDefaultAsync(m=> m.Id == id);
        }

        public async Task<PagedList<Message>> GetMessagesForUser(MessageParams messageParams)
        {
            var messages = _context.Messages
                .Include(u=> u.Sender)
                .ThenInclude(p=> p.Photos)
                .Include(u=> u.Recipient)
                .ThenInclude(p=>p.Photos)
                .AsQueryable();

            switch(messageParams.MessageContainer)
            {
                case "Inbox":
                    messages = messages.Where(u=> u.RecipientId == messageParams.UserId && !u.RecipientDeleted);
                    break;
                case "Outbox":
                    messages = messages.Where(u=> u.SenderId == messageParams.UserId && !u.SenderDeleted);
                    break;
                default: 
                    messages = messages.Where(u=> u.RecipientId == messageParams.UserId && !u.IsRead && !u.RecipientDeleted);
                    break;
            }

            messages = messages.OrderByDescending(m=> m.MessageSent);

            return await PagedList<Message>.CreateAsync(messages, messageParams.PageNumber, messageParams.PageSize);
        }

        public async Task<IEnumerable<Message>> GetMessageThread(int userId, int recipientId)
        {
            var messages = await _context.Messages
               .Include(u => u.Sender).ThenInclude(p => p.Photos)
               .Include(u => u.Recipient).ThenInclude(p => p.Photos)
               .Where(m => m.RecipientId == userId &&!m.RecipientDeleted && m.SenderId == recipientId 
                || m.RecipientId == recipientId && m.SenderId == userId && m.SenderDeleted == false)
               .OrderByDescending(m=> m.MessageSent)
               .ToListAsync();

            return messages;
        }
    }
}