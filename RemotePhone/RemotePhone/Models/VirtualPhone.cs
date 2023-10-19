using System.ComponentModel.DataAnnotations;

namespace RemotePhone.Models
{
    public class VirtualPhone
    {
        [Key]
        public int Port { set; get; }
        public string? UserId { set; get; }
        public bool? InUse { set; get; }
        public DateTime Lastvisit { set; get; }


        public override bool Equals(object p)
        {
            if ((p as VirtualPhone) == null)
                return false;
            return (this.Port) == ((VirtualPhone)p).Port;
        }

        public override int GetHashCode()
        {
            return this.Port.GetHashCode();
        }
    }
}
