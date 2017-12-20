﻿using Inceptum.Messaging.Contract;

namespace Inceptum.Messaging.Configuration
{
    public interface IEndpointProvider
    {
        bool Contains(string endpointName);
        Endpoint Get(string endpointName);
    }
}