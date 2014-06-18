using System;

namespace MusicXml2
{
    public class InvalidSkipException : Exception
    {
        public InvalidSkipException(Step step) : base(step + " is not a valid step") { }
    }
}
