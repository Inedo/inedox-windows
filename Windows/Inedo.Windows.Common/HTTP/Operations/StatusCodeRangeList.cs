using System;
using System.Collections.Generic;
using System.Linq;

namespace Inedo.Extensions.HTTP.Operations
{
    internal sealed class StatusCodeRangeList
    {
        private List<Range> ranges;

        public static StatusCodeRangeList Parse(string value)
        {
            var values = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            return new StatusCodeRangeList { ranges = values.Select(v => Range.Parse(v)).Where(r => r != null).ToList() };
        }

        private StatusCodeRangeList()
        {
        }

        public bool IsInAnyRange(int code)
        {
            return this.ranges.Any(r => r.IsInRange(code));
        }

        public override string ToString()
        {
            return string.Join(",", this.ranges.Select(r => r.ToString()));
        }

        private sealed class Range
        {
            private int start;
            private int end;

            public static Range Parse(string value)
            {
                try
                {
                    if (!value.Contains(":"))
                        return new Range(int.Parse(value));

                    var split = value.Split(new[] { ':' }, 2, StringSplitOptions.RemoveEmptyEntries);
                    return new Range(int.Parse(split[0]), int.Parse(split[1]));
                }
                catch
                {
                }

                return null;
            }

            private Range(int singleValue) : this(singleValue, singleValue) { }

            private Range(int start, int end)
            {
                if (start > end)
                    throw new ArgumentException("start > end");
                this.start = start;
                this.end = end;
            }

            public bool IsInRange(int value)
            {
                return this.start <= value && value <= this.end;
            }

            public override string ToString()
            {
                if (start == end)
                    return start.ToString();
                else
                    return string.Format("{0}:{1}", this.start, this.end);
            }
        }
    }
}
