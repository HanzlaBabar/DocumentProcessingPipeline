using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentProcessingPipeline.Core.Entities
{
    public class Tag
    {
        public string Label { get; set; }

        public string Type { get; set; }

        public int X { get; set; }

        public int Y { get; set; }
    }
}
