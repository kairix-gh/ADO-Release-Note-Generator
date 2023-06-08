using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADO_Release_Note_Generator_Shared.Models {
    public class ReleaseNoteImageInfo {
        public int Width { get; set; }
        public int Height { get; set; }
        public string Url { get; set; } = "";
        public byte[] Bytes { get; set; } = new byte[0];
    }
}