using Microsoft.AspNetCore.Http;
using ProjekatNBP.Session;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProjekatNBP.Extensions
{
    public static class SessionExtensions
    {
        public static bool IsUsernameEmpty(this ISession session)
        {
            return string.IsNullOrEmpty(session.GetString(SessionKeys.Username));
        }
    }
}
