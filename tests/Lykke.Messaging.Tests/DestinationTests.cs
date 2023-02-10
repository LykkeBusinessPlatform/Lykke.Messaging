using System;
using Lykke.Messaging.Contract;
using NUnit.Framework;

namespace Lykke.Messaging.Tests
{
    [TestFixture]
    internal class DestinationTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void Create_When_BothAddressesAreNullOrEmpty_Then_ThrowException(string address)
        {
            Assert.Throws<ArgumentNullException>(() => new Destination(address));
            Assert.Throws<ArgumentNullException>(() => new Destination(address, address));
        }
        
        [Test]
        public void Destinations_AreEqual_When_BothAddressesAreEqual()
        {
            var destination1 = new Destination("address1");
            var destination2 = new Destination("address1");
            
            Assert.AreEqual(destination1, destination2);
        }
    }
}