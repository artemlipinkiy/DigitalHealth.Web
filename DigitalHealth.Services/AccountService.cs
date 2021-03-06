﻿using DigitalHealth.GlobalInterfaces;
using DigitalHealth.Web.Entities;
using DigitalHealth.Web.EntitiesDto;
using Microsoft.Owin.Security;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace DigitalHealth.Services
{
    public class AccountService : IAccountService
    {
        private readonly ILogger _logger;

        public AccountService(ILogger logger)
        {
            _logger = logger;
        }
        public async Task<bool> Login(AccountLoginDto dto, IAuthenticationManager AuthenticationManager)
        {
            try
            {
                using (DHContext db = new DHContext())
                {
                    User user = await db.Users.Include(r => r.Role).Where(u => u.Login == dto.Login).FirstOrDefaultAsync();
                    if (user == null)
                    {
                        return false;
                    }
                    else
                    {
                        if (await VerifyHashedPassword(user.HashPassword, dto.Password))
                        {
                            ClaimsIdentity claim = new ClaimsIdentity("ApplicationCookie", ClaimsIdentity.DefaultNameClaimType, ClaimsIdentity.DefaultRoleClaimType);
                            claim.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString(), ClaimValueTypes.String));
                            claim.AddClaim(new Claim(ClaimsIdentity.DefaultNameClaimType, user.Login, ClaimValueTypes.String));
                            claim.AddClaim(new Claim("http://schemas.microsoft.com/accesscontrolservice/2010/07/claims/identityprovider",
                                "OWIN Provider", ClaimValueTypes.String));
                            if (user.Role != null)
                                claim.AddClaim(new Claim(ClaimsIdentity.DefaultRoleClaimType, user.Role.Name, ClaimValueTypes.String));

                            AuthenticationManager.SignOut();
                            AuthenticationManager.SignIn(new AuthenticationProperties
                            {
                                IsPersistent = true
                            }, claim);
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                _logger.Error($"Failed Login for {dto.Login} : {exc}");
                return false;
            }
            
        }
        public async Task Logout(IAuthenticationManager AuthenticationManager)
        {
            try
            {
                AuthenticationManager.SignOut();
            }
            catch (Exception exc)
            {
                _logger.Error($"Failed logout {AuthenticationManager.User.Identity} : {exc}");
                throw;
            }
        }
        public async Task<bool> LoginExist(string Login)
        {
            try
            {
                using (DHContext db = new DHContext())
                {
                    var userdb = await db.Users.Where(user => user.Login == Login).FirstOrDefaultAsync();
                    if (userdb != null)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            catch (Exception exc)
            {
                _logger.Error($"Failed check login exist {Login}: {exc}");
                throw;
            }
            
        }
        public async Task<bool> PasswordMatch(string password, string repeatpassword)
        {
            if (password == repeatpassword)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        public async Task<RoleDto> GetRole(string name)
        {
            try
            {
                using (DHContext db = new DHContext())
                {
                    return await db.Roles.Where(r => r.Name == name).Select(r => new RoleDto
                    {
                        Description = r.Description,
                        Id = r.Id,
                        Name = r.Name
                    }).FirstOrDefaultAsync();
                }
            }
            catch (Exception exc)
            {
                _logger.Error($"Failed GetRole for {name}: {exc}");
                throw;
            }
           
        }
        public async Task<bool> Register(AccountRegisterDto dto)
        {
            try
            {
                Guid Userid = Guid.NewGuid();
                Guid ProfileId = Guid.NewGuid();
                RoleDto defaultRole = await GetRole("Default");
                using (DHContext db = new DHContext())
                {
                    Profile profile = new Profile
                    {
                        Id = ProfileId,
                        FirstName = string.Empty,
                        Gender = string.Empty,
                        LastName = string.Empty,
                        MiddleName = string.Empty,
                        UserId = Userid
                    };
                    User user = new User
                    {
                        Login = dto.Login,
                        Id = Userid,
                        RoleId = defaultRole.Id,
                        HashPassword = HashPassword(dto.Password),
                        ProfileId = ProfileId
                    };
                    db.Users.Add(user);
                    db.Profiles.Add(profile);
                    await db.SaveChangesAsync();
                }
                return true;
            }
            catch (Exception exc)
            {
                _logger.Error($"Failed register new user {dto.Login} : {exc}");
                return false;
            }
           
        }
        public async Task<ProfileDto> GetProfile(Guid userId)
        {
            try
            {
                using (DHContext db = new DHContext())
                {
                    return await db.Profiles.Where(p => p.UserId == userId).Select(p => new ProfileDto
                    {
                        Id = p.Id,
                        FirstName = p.FirstName,
                        LastName = p.LastName,
                        MiddleName = p.MiddleName,
                        Gender = p.Gender
                    }).FirstOrDefaultAsync();
                }
            }
            catch (Exception exc)
            {
                _logger.Error($"Failed get profile for user {userId} : {exc}");
                throw;
            }
           
        }
        public async Task<Guid> GetUserId(string name)
        {
            try
            {
                using (DHContext db = new DHContext())
                {
                    return await db.Users.Where(u => u.Login == name).Select(u => u.Id).FirstOrDefaultAsync();
                }
            }
            catch (Exception exc)
            {
                _logger.Error($"Failed get user id by name {name} : {exc}");
                throw;
            }
            
        }

        public async Task UpdateProfile(ProfileDto dto)
        {
            try
            {
                using (DHContext db = new DHContext())
                {
                    var entity = await db.Profiles.Where(p => p.Id == dto.Id).FirstOrDefaultAsync();
                    entity.FirstName = dto.FirstName;
                    entity.LastName = dto.LastName;
                    entity.MiddleName = dto.MiddleName;
                    entity.Gender = dto.Gender;
                    db.Entry(entity).State = EntityState.Modified;
                    await db.SaveChangesAsync();
                }
            }
            catch (Exception exc)
            {
                _logger.Error($"Failed update profile {dto.Id} : {exc}");
                throw;
            }
            
        }
        #region Password
        private static string HashPassword(string password)
        {
            byte[] salt;
            byte[] buffer2;
            if (password == null)
            {
                throw new ArgumentNullException("password");
            }
            using (Rfc2898DeriveBytes bytes = new Rfc2898DeriveBytes(password, 0x10, 0x3e8))
            {
                salt = bytes.Salt;
                buffer2 = bytes.GetBytes(0x20);
            }
            byte[] dst = new byte[0x31];
            Buffer.BlockCopy(salt, 0, dst, 1, 0x10);
            Buffer.BlockCopy(buffer2, 0, dst, 0x11, 0x20);
            return Convert.ToBase64String(dst);
        }
        public async Task<bool> VerifyHashedPassword(string hashedPassword, string password)
        {
            try
            {
                byte[] buffer4;
                if (hashedPassword == null)
                {
                    return false;
                }
                if (password == null)
                {
                    throw new ArgumentNullException("password");
                }
                byte[] src = Convert.FromBase64String(hashedPassword);
                if ((src.Length != 0x31) || (src[0] != 0))
                {
                    return false;
                }
                byte[] dst = new byte[0x10];
                Buffer.BlockCopy(src, 1, dst, 0, 0x10);
                byte[] buffer3 = new byte[0x20];
                Buffer.BlockCopy(src, 0x11, buffer3, 0, 0x20);
                using (Rfc2898DeriveBytes bytes = new Rfc2898DeriveBytes(password, dst, 0x3e8))
                {
                    buffer4 = bytes.GetBytes(0x20);
                }
                return ByteArraysEqual(buffer3, buffer4);
            }
            catch (Exception exc)
            {
                _logger.Error($"Failed verify hashed password : {exc}");
                return false;
            }
           
        }
        private static bool ByteArraysEqual(byte[] b1, byte[] b2)
        {
            if (b1 == b2) return true;
            if (b1 == null || b2 == null) return false;
            if (b1.Length != b2.Length) return false;
            for (int i = 0; i < b1.Length; i++)
            {
                if (b1[i] != b2[i]) return false;
            }
            return true;
        }
        #endregion
    }
}
