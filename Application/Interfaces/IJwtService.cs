using System.Collections.Generic;

namespace Application.Interfaces
{
    public interface IJwtService
    {
        string GenerateToken(string userId, string email, IList<string> roles);
    }
}
