using System.ComponentModel.DataAnnotations;

namespace RemotePhone.Models
{
    public class User
    {
        [Key]
        public string? UserId { get; set; }
        public string? ConnectionId { get; set; }
        public bool InCall;
    }
}
