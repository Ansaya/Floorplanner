using System;
using System.Runtime.Serialization;

namespace Floorplanner.Solver
{
    [Serializable]
    public class OptimizationException : Exception, ISerializable
    {
        public OptimizationException()
        {
        }

        public OptimizationException(string message) : base(message)
        {
        }

        public OptimizationException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected OptimizationException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
