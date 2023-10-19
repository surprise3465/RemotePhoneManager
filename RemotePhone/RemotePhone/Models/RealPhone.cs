using System.ComponentModel.DataAnnotations;

namespace RemotePhone.Models
{
    public class RealPhone
    {
        [Key]
        public string Serial { set; get; }
        public string? UserId { set; get; }
        public bool? InUse { set; get; }
        public DateTime Lastvisit { set; get; }


        public override bool Equals(object p)
        {
            if ((p as RealPhone) == null)
                return false;
            return (this.Serial) == ((RealPhone)p).Serial;
        }

        public override int GetHashCode()
        {
            return Serial.GetHashCode();
        }
    }
}
