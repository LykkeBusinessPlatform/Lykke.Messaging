using System.Collections.Generic;

using Castle.MicroKernel.Registration;
using Castle.Windsor;

using Lykke.Messaging.Castle;
using Lykke.Messaging.Configuration;
using Lykke.Messaging.Contract;

using NUnit.Framework;

namespace Lykke.Messaging.Tests.Castle
{
    [TestFixture]
    public class DependsOnEndpointsTests
    {
        private Endpoint m_Endpoint1;
        private Endpoint m_Endpoint2;
        private Endpoint m_Endpoint3;
        private Endpoint m_Endpoint4;
        private Endpoint m_Endpoint5;
        private WindsorContainer m_Container;

        [SetUp]
        public void SetUp()
        {
            m_Endpoint1 = new Endpoint("transport-id-1", "destination-1");
            m_Endpoint2 = new Endpoint("transport-id-2", "destination-2");
            m_Endpoint3 = new Endpoint("transport-id-3", "destination-3");
            m_Endpoint4 = new Endpoint("transport-id-4", "destination-4");
            m_Endpoint5 = new Endpoint("transport-id-5", "destination-5");
            var endpointResolver = new EndpointResolver(new Dictionary<string, Endpoint>()
                {
                    {"endpoint1", m_Endpoint1},
                    {"endpoint2", m_Endpoint2},
                    {"endpoint3", m_Endpoint3},
                    {"endpoint4", m_Endpoint4},
                    {"endpoint5", m_Endpoint5},
                });

            m_Container = new WindsorContainer();
            m_Container.Kernel.Resolver.AddSubResolver(endpointResolver);
        }

        [Test]
        public void EndpointResolveByConstructorParameterNameTest()
        {
            m_Container.Register(Component.For<EndpontTest1>());

            var test1 = m_Container.Resolve<EndpontTest1>();
            Assert.That(m_Endpoint1.TransportId, Is.EqualTo(test1.Endpoint.TransportId));
            Assert.That(m_Endpoint1.Destination, Is.EqualTo(test1.Endpoint.Destination));
        }

        [Test]
        public void EndpointResolveByOverridenParameterNameTest()
        {
            m_Container.Register(Component.For<EndpontTest1>().WithEndpoints(new { endpoint1 = "endpoint2" }));

            var test1 = m_Container.Resolve<EndpontTest1>();
            Assert.That(m_Endpoint2.TransportId, Is.EqualTo(test1.Endpoint.TransportId));
            Assert.That(m_Endpoint2.Destination, Is.EqualTo(test1.Endpoint.Destination));
        }

        [Test]
        public void EndpointResolveByExplicitEndpointParameterNameTest()
        {
            var endpoint = new Endpoint(transportId: "custom-transport-id", destination: "custom-destination");

            m_Container.Register(Component.For<EndpontTest1>().WithEndpoints(new { endpoint1 = endpoint }));
            var test1 = m_Container.Resolve<EndpontTest1>();
            Assert.That(endpoint.TransportId, Is.EqualTo(test1.Endpoint.TransportId));
            Assert.That(endpoint.Destination, Is.EqualTo(test1.Endpoint.Destination));
        }

        [Test]
        public void EndpointResolveByTwoDifferentConstructorParameterNameTest()
        {
            m_Container.Register(Component.For<EndpointTest2>());

            var test1 = m_Container.Resolve<EndpointTest2>();
            Assert.That(m_Endpoint1.TransportId, Is.EqualTo(test1.Endpoint1.TransportId));
            Assert.That(m_Endpoint1.Destination, Is.EqualTo(test1.Endpoint1.Destination));
            Assert.That(m_Endpoint2.TransportId, Is.EqualTo(test1.Endpoint2.TransportId));
            Assert.That(m_Endpoint2.Destination, Is.EqualTo(test1.Endpoint2.Destination));
        }

        [Test]
        public void EndpointResolveByTwoDifferentOverridenParameterNameTest()
        {
            m_Container.Register(Component.For<EndpointTest2>().WithEndpoints(new { endpoint1 = "endpoint4", endpoint2 = "endpoint5" }));

            var test1 = m_Container.Resolve<EndpointTest2>();
            Assert.That(m_Endpoint4.TransportId, Is.EqualTo(test1.Endpoint1.TransportId));
            Assert.That(m_Endpoint4.Destination, Is.EqualTo(test1.Endpoint1.Destination));
            Assert.That(m_Endpoint5.TransportId, Is.EqualTo(test1.Endpoint2.TransportId));
            Assert.That(m_Endpoint5.Destination, Is.EqualTo(test1.Endpoint2.Destination));
        }

        [Test]
        public void EndpointResolveByTwoDifferentOneOverridenParameterNameTest()
        {
            m_Container.Register(Component.For<EndpointTest2>().WithEndpoints(new { endpoint2 = "endpoint5" }));

            var test1 = m_Container.Resolve<EndpointTest2>();
            Assert.That(m_Endpoint1.TransportId, Is.EqualTo(test1.Endpoint1.TransportId));
            Assert.That(m_Endpoint1.Destination, Is.EqualTo(test1.Endpoint1.Destination));
            Assert.That(m_Endpoint5.TransportId, Is.EqualTo(test1.Endpoint2.TransportId));
            Assert.That(m_Endpoint5.Destination, Is.EqualTo(test1.Endpoint2.Destination));
        }

        [Test]
        public void EndpointResolveByTwoDifferentOneOverridenAndExplicitParameterNameTest()
        {
            var endpoint = new Endpoint(transportId: "custom-transport-id", destination: "custom-destination");

            m_Container.Register(Component.For<EndpointTest2>().WithEndpoints(new { endpoint1 = "endpoint4", endpoint2 = endpoint }));

            var test1 = m_Container.Resolve<EndpointTest2>();
            Assert.That(m_Endpoint4.TransportId, Is.EqualTo(test1.Endpoint1.TransportId));
            Assert.That(m_Endpoint4.Destination, Is.EqualTo(test1.Endpoint1.Destination));
            Assert.That(endpoint.TransportId, Is.EqualTo(test1.Endpoint2.TransportId));
            Assert.That(endpoint.Destination, Is.EqualTo(test1.Endpoint2.Destination));
        }
    }

    internal class EndpontTest1
    {
        public Endpoint Endpoint { get; private set; }

        public EndpontTest1(Endpoint endpoint1)
        {
            Endpoint = endpoint1;
        }
    }

    internal class EndpointTest2
    {
        public Endpoint Endpoint1 { get; private set; }
        public Endpoint Endpoint2 { get; private set; }

        public EndpointTest2(Endpoint endpoint1, Endpoint endpoint2)
        {
            Endpoint1 = endpoint1;
            Endpoint2 = endpoint2;
        }
    }
}