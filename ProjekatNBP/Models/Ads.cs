using System.Collections.Generic;

namespace ProjekatNBP.Models
{
	public class Ads
    {
        public List<Category> CategoryList { get; set; }
        public List<Ad> AdList { get; set; }
        public List<Ad> AdRecomendList { get; set; }
        public (UserInfo userInfo, int score)[] Leaderboard { get; set; }
    }
}
