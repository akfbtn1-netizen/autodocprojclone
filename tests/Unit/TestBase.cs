using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using System;

namespace Tests.Unit
{
    /// <summary>
    /// Base class for all unit tests providing common test infrastructure.
    /// </summary>
    public abstract class TestBase
    {
        protected Mock<ILogger<T>> CreateMockLogger<T>()
        {
            return new Mock<ILogger<T>>();
        }
        
        protected ILogger<T> CreateLogger<T>()
        {
            return CreateMockLogger<T>().Object;
        }
    }
}