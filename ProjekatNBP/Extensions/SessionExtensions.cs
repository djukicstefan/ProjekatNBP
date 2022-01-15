using Microsoft.AspNetCore.Http;
using ProjekatNBP.Session;

namespace ProjekatNBP.Extensions {
    public static class SessionExtensions
    {
        public static bool IsUsernameEmpty(this ISession session) 
            => string.IsNullOrEmpty(GetUsername(session));

        public static bool IsLoggedIn(this ISession session)
            => !IsUsernameEmpty(session) && GetUserId(session) != -1;

        public static string GetUsername(this ISession session)
            => session.GetString(SessionKeys.Username);

        public static int GetUserId(this ISession session)
            => session.GetInt32(SessionKeys.UserId) ?? -1;
    }
}
