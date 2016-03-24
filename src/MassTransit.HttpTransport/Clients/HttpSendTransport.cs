﻿// Copyright 2007-2016 Chris Patterson, Dru Sellers, Travis Smith, et. al.
//  
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace MassTransit.HttpTransport.Clients
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;
    using MassTransit.Pipeline;
    using Transports;


    public class HttpSendTransport :
        ISendTransport
    {
        static readonly ILog _log = Logger.Get<HttpSendTransport>();
        readonly ClientCache _clientCache;
        readonly SendObservable _observers;
        readonly HttpSendSettings _sendSettings;

        public HttpSendTransport(ClientCache clientCache, HttpSendSettings sendSettings)
        {
            _clientCache = clientCache;
            _sendSettings = sendSettings;
            _observers = new SendObservable();
        }

        public ConnectHandle ConnectSendObserver(ISendObserver observer)
        {
            return _observers.Connect(observer);
        }

        public async Task Send<T>(T message, IPipe<SendContext<T>> pipe, CancellationToken cancelSend) where T : class
        {
            IPipe<ClientContext> clientPipe = Pipe.New<ClientContext>(p =>
            {
                //TODO: p.UseFilter(_filter);

                p.UseExecuteAsync(async clientContext =>
                {
                    //sendSettings
                    var method = HttpMethod.Post;
                    var timeOut = TimeSpan.FromSeconds(5);

                    var context = new HttpSendContextImpl<T>(message, cancelSend);

                    try
                    {
                        await pipe.Send(context).ConfigureAwait(false);

                        using (var msg = new HttpRequestMessage(_sendSettings.Method, context.DestinationAddress))
                        using (var payload = new ByteArrayContent(context.Body))
                        {
                            //TODO: Get access to a HostInfo instance
                            msg.Headers.UserAgent.Add(new ProductInfoHeaderValue("MassTransit", "3"));

                            if (context.ResponseAddress != null)
                                msg.Headers.Referrer = context.ResponseAddress;

                            payload.Headers.ContentType = new MediaTypeHeaderValue(context.ContentType.MediaType);

                            foreach (
                                KeyValuePair<string, object> header in
                                    context.Headers.GetAll().Where(h => h.Value != null && (h.Value is string || h.Value.GetType().IsValueType)))
                            {
                                msg.Headers.Add(header.Key, header.Value.ToString());
                            }

                            if (context.MessageId.HasValue)
                                msg.Headers.Add(HttpHeaders.MessageId, context.MessageId.Value.ToString());

                            if (context.CorrelationId.HasValue)
                                msg.Headers.Add(HttpHeaders.CorrelationId, context.CorrelationId.Value.ToString());

                            if(context.InitiatorId.HasValue)
                                msg.Headers.Add(HttpHeaders.InitiatorId, context.InitiatorId.Value.ToString());

                            if (context.ConversationId.HasValue)
                                msg.Headers.Add(HttpHeaders.ConversationId, context.ConversationId.Value.ToString());

                            if(context.RequestId.HasValue)
                                msg.Headers.Add(HttpHeaders.RequestId, context.RequestId.Value.ToString());

                            //TODO: TTL?

                            msg.Content = payload;

                            await _observers.PreSend(context).ConfigureAwait(false);

                            var r = await clientContext.SendAsync(msg, cancelSend).ConfigureAwait(false);

                            await _observers.PostSend(context).ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        await _observers.SendFault(context, ex).ConfigureAwait(false);

                        if (_log.IsErrorEnabled)
                            _log.Error("Send Fault: " + context.DestinationAddress, ex);

                        throw;
                    }
                });
            });

            await _clientCache.DoWith(clientPipe, cancelSend).ConfigureAwait(false);
        }

        public Task Move(ReceiveContext context, IPipe<SendContext> pipe)
        {
            return Task.FromResult(true);
        }

        public Task Close()
        {
            return Task.FromResult(0);
        }
    }


    public class HttpHeaders
    {
        public const string InitiatorId = "MassTransit-Initiator-Id";
        public const string RequestId = "MassTransit-Request-Id";
        public const string ConversationId = "MassTransit-Conversation-Id";
        public const string MessageId = "MassTransit-Message-Id";
        public const string CorrelationId = "MassTransit-Message-Id";
    }

}