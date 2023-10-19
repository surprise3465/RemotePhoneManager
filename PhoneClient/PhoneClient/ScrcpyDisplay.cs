using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhoneClient
{
    public class FrameDataClass
    {
        public int Width { set; get; }
        public int Height { set; get; }
        public int FrameNumber { set; get; }
        public byte[] Data { set; get; }
        public int Length { set; get; }
        public int AVPixelFormat { set; get; }
    }
}
