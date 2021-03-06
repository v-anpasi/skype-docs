﻿using Microsoft.Rtc.Internal.Platform.ResourceContract;
using Microsoft.Rtc.Internal.RestAPI.Common.MediaTypeFormatters;
using Microsoft.SfB.PlatformService.SDK.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Rtc.Internal.RestAPI.ResourceModel;

namespace Microsoft.SfB.PlatformService.SDK.ClientModel
{
    internal class Communication : BasePlatformResource<CommunicationResource, CommunicationCapability>, ICommunication
    {
        #region Private fields

        /// <summary>
        /// Conversations
        /// </summary>
        private readonly ConcurrentDictionary<string, Conversation> m_conversations;

        /// <summary>
        /// invitations
        /// </summary>
        private readonly ConcurrentDictionary<string, IInvitation> m_invites;

        /// <summary>
        /// invitations TCS: invites thread Id &lt;---&gt; Invites Tcs, this is to track the incoming invite comes
        /// </summary>
        private readonly ConcurrentDictionary<string, TaskCompletionSource<IInvitation>> m_inviteAddedTcses;

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="restfulClient"></param>
        /// <param name="resource"></param>
        /// <param name="baseUri"></param>
        /// <param name="resourceUri"></param>
        internal Communication(IRestfulClient restfulClient, CommunicationResource resource, Uri baseUri, Uri resourceUri, Application parent)
                : base(restfulClient, resource, baseUri, resourceUri, parent)
        {
            m_conversations = new ConcurrentDictionary<string, Conversation>();
            m_invites = new ConcurrentDictionary<string, IInvitation>();
            m_inviteAddedTcses = new ConcurrentDictionary<string, TaskCompletionSource<IInvitation>>();
        }

        #endregion

        #region Public methods

        /// <summary>
        /// Start messaging
        /// </summary>
        /// <param name="subject"></param>
        /// <param name="to"></param>
        /// <param name="callbackUrl"></param>
        /// <returns></returns>
        public Task<IMessagingInvitation> StartMessagingAsync(string subject, string to, string callbackUrl, LoggingContext loggingContext = null)
        {
            Logger.Instance.Information(string.Format("[Communication] calling startMessaging. LoggingContext: {0}",
                 loggingContext == null ? string.Empty : loggingContext.ToString()));

            string href = PlatformResource?.StartMessagingLink?.Href;

            if (string.IsNullOrWhiteSpace(href))
            {
                throw new CapabilityNotAvailableException("Link to start messaging is not available.");
            }

            return StartMessagingWithIdentityAsync(subject, to, callbackUrl, href, null, null, loggingContext);
        }

        /// <summary>
        /// Start messaging
        /// </summary>
        /// <param name="subject"></param>
        /// <param name="to"></param>
        /// <param name="callbackUrl"></param>
        /// <param name="localUserDisplayName"></param>
        /// <param name="localUserUri"></param>
        /// <returns></returns>
        public Task<IMessagingInvitation> StartMessagingWithIdentityAsync(string subject, string to, string callbackUrl, string localUserDisplayName, string localUserUri, LoggingContext loggingContext = null)
        {
            Logger.Instance.Information(string.Format("[Communication] calling startMessagingWithIdentity. LoggingContext: {0}",
                 loggingContext == null ? string.Empty : loggingContext.ToString()));

            string href = PlatformResource?.StartMessagingWithIdentityLink?.Href;

            if (string.IsNullOrWhiteSpace(href))
            {
                throw new CapabilityNotAvailableException("Link to start messaging with identity is not available.");
            }

            return StartMessagingWithIdentityAsync(subject, to, callbackUrl, href, localUserDisplayName, localUserUri, loggingContext);
        }

        /// <summary>
        /// Start audio video call
        /// </summary>
        /// <param name="subject"></param>
        /// <param name="to"></param>
        /// <param name="callbackUrl"></param>
        /// <returns></returns>
        public Task<IAudioVideoInvitation> StartAudioVideoAsync(string subject, string to, string callbackUrl, LoggingContext loggingContext = null)
        {
            Logger.Instance.Information(string.Format("[Communication] calling startAudioVideo. LoggingContext: {0}",
                 loggingContext == null ? string.Empty : loggingContext.ToString()));

            string href = PlatformResource?.StartAudioVideoLink?.Href;

            if (string.IsNullOrWhiteSpace(href))
            {
                throw new CapabilityNotAvailableException("Link to start AudioVideoCall is not available.");
            }

            return StartAudioVideoAsync(href, subject, to, callbackUrl, loggingContext);
        }

        /// <summary>
        /// Start audio video call
        /// </summary>
        /// <param name="subject"></param>
        /// <param name="to"></param>
        /// <param name="callbackUrl"></param>
        /// <returns></returns>
        public Task<IAudioVideoInvitation> StartAudioAsync(string subject, string to, string callbackUrl, LoggingContext loggingContext = null)
        {
            Logger.Instance.Information(string.Format("[Communication] calling startAudio. LoggingContext: {0}",
                 loggingContext == null ? string.Empty : loggingContext.ToString()));

            string href = PlatformResource?.StartAudioLink?.Href;

            if (string.IsNullOrWhiteSpace(href))
            {
                throw new CapabilityNotAvailableException("Link to start audio is not available.");
            }

            return StartAudioVideoAsync(href, subject, to, callbackUrl, loggingContext);
        }

        public override bool Supports(CommunicationCapability capability)
        {
            string href = null;
            switch (capability)
            {
                case CommunicationCapability.StartMessaging:
                    {
                        href = PlatformResource?.StartMessagingLink?.Href;
                        break;
                    }
                case CommunicationCapability.StartMessagingWithIdentity:
                    {
                        href = PlatformResource?.StartMessagingWithIdentityLink?.Href;
                        break;
                    }
                case CommunicationCapability.StartAudioVideo:
                    {
                        href = PlatformResource?.StartAudioVideoLink?.Href;
                        break;
                    }
                case CommunicationCapability.StartAudio:
                    {
                        href = PlatformResource?.StartAudioLink?.Href;
                        break;
                    }
            }

            return !string.IsNullOrWhiteSpace(href);
        }

        #endregion

        #region Internal methods

        /// <summary>
        /// Dispatch events to conversations
        /// </summary>
        /// <param name="eventContexts"></param>
        internal void DispatchConversationEvents(List<EventContext> eventContexts)//Suppose sender of eventContext list should be same
        {
            if (eventContexts == null || eventContexts.Count == 0)
            {
                return;
            }
            EventContext eventDefault = eventContexts.FirstOrDefault();
            string conversationUri = UriHelper.NormalizeUriWithNoQueryParameters(eventDefault.SenderHref, eventDefault.BaseUri);
            Conversation conversation = null;
            m_conversations.TryGetValue(conversationUri, out conversation);
            if (conversation != null)
            {
                foreach (EventContext e in eventContexts)
                {
                    conversation.ProcessAndDispatchEventsToChild(e);
                }
            }
        }

        /// <summary>
        /// ProcessAndDispatchEventsToChild implementation
        /// </summary>
        /// <param name="eventContext"></param>
        /// <returns></returns>
        internal override bool ProcessAndDispatchEventsToChild(EventContext eventContext)
        {
            //There is no child for events with sender = communication
            Logger.Instance.Information(string.Format("[Communication] get incoming communication event, sender: {0}, senderHref: {1}, EventResourceName: {2} EventFullHref: {3}, EventType: {4} ,LoggingContext: {5}",
                        eventContext.SenderResourceName, eventContext.SenderHref, eventContext.EventResourceName, eventContext.EventFullHref, eventContext.EventEntity.Relationship.ToString(), eventContext.LoggingContext == null ? string.Empty : eventContext.LoggingContext.ToString()));

            if (string.Equals(eventContext.EventEntity.Link.Token, TokenMapper.GetTokenName(typeof(ConversationResource))))
            {
                string conversationNormalizedUri = UriHelper.NormalizeUriWithNoQueryParameters(eventContext.EventEntity.Link.Href, eventContext.BaseUri);
                Conversation currentConversation = m_conversations.GetOrAdd(conversationNormalizedUri,
                    (a) =>
                    {
                        Logger.Instance.Information(string.Format("[Communication] Add conversation {0} LoggingContext: {1}",
                        conversationNormalizedUri, eventContext.LoggingContext == null ? string.Empty : eventContext.LoggingContext.ToString()));

                        ConversationResource localResource = this.ConvertToPlatformServiceResource<ConversationResource>(eventContext);
                        //For every conversation resource, we want to make sure it is using latest rest ful client
                        return new Conversation(this.RestfulClient, localResource, eventContext.BaseUri, eventContext.EventFullHref, this);
                    }
                    );

                //Remove from cache if it is a delete operation
                if (eventContext.EventEntity.Relationship == EventOperation.Deleted)
                {
                    Conversation removedConversation = null;
                    Logger.Instance.Information(string.Format("[Communication] Remove conversation {0} LoggingContext: {1}",
                           conversationNormalizedUri, eventContext.LoggingContext == null ? string.Empty : eventContext.LoggingContext.ToString()));
                    m_conversations.TryRemove(conversationNormalizedUri, out removedConversation);
                }

                currentConversation.HandleResourceEvent(eventContext);

                return true;
            }
            else if (string.Equals(eventContext.EventEntity.Link.Token, TokenMapper.GetTokenName(typeof(MessagingInvitationResource))))
            {
                this.HandleInvitationEvent<MessagingInvitationResource>(
                    eventContext,
                    (localResource) => new MessagingInvitation(this.RestfulClient, localResource, eventContext.BaseUri, eventContext.EventFullHref, this)
                 );
                return true;
            }
            else if (string.Equals(eventContext.EventEntity.Link.Token, TokenMapper.GetTokenName(typeof(AudioVideoInvitationResource))))
            {
                this.HandleInvitationEvent<AudioVideoInvitationResource>(
                    eventContext,
                    (localResource) => new AudioVideoInvitation(this.RestfulClient, localResource, eventContext.BaseUri, eventContext.EventFullHref, this)
                );
                return true;
            }
            else if (string.Equals(eventContext.EventEntity.Link.Token, TokenMapper.GetTokenName(typeof(OnlineMeetingInvitationResource))))
            {
                this.HandleInvitationEvent<OnlineMeetingInvitationResource>(
                    eventContext,
                    (localResource) => new OnlineMeetingInvitation(this.RestfulClient, localResource, eventContext.BaseUri, eventContext.EventFullHref, this)
                );
                return true;
            }
            else if (string.Equals(eventContext.EventEntity.Link.Token, TokenMapper.GetTokenName(typeof(ParticipantInvitationResource))))
            {
                this.HandleInvitationEvent<ParticipantInvitationResource>(
                    eventContext,
                    (localResource) => new ParticipantInvitation(this.RestfulClient, localResource, eventContext.BaseUri, eventContext.EventFullHref, this)
                );
                return true;
            }
            //TODO: Process , audioVideoInvitation, ...
            else
            {
                return false;
            }
        }

        private void HandleInvitationEvent<T>(EventContext eventcontext, Func<T, IInvitation> inviteGenerateDelegate) where T : InvitationResource
        {
            string NormalizedUri = UriHelper.NormalizeUriWithNoQueryParameters(eventcontext.EventEntity.Link.Href, eventcontext.BaseUri);
            T localResource = this.ConvertToPlatformServiceResource<T>(eventcontext);
            IInvitation invite = m_invites.GetOrAdd(localResource.OperationContext, (a) =>
            {
                Logger.Instance.Information(string.Format("[Communication] Started and Add invitation: OperationContext:{0}, Href: {1} , LoggingContext: {2}",
                    localResource.OperationContext, NormalizedUri, eventcontext.LoggingContext == null ? string.Empty : eventcontext.LoggingContext.ToString()));

                return inviteGenerateDelegate(localResource);
            });

            if (invite.RelatedConversation == null)
            {
                //Populate conversation resource if needed
                string relatedConversationNormalizedUri = UriHelper.NormalizeUriWithNoQueryParameters(localResource.ConversationResourceLink.Href, eventcontext.BaseUri);
                Uri relatedConversationUri = UriHelper.CreateAbsoluteUri(eventcontext.BaseUri, localResource.ConversationResourceLink.Href);
                Conversation relatedConversation = m_conversations.GetOrAdd(relatedConversationNormalizedUri,
                    (a) =>
                    {
                        Logger.Instance.Information(string.Format("[Communication] Add conversation {0} LoggingContext: {1}",
                            relatedConversationNormalizedUri, eventcontext.LoggingContext == null ? string.Empty : eventcontext.LoggingContext.ToString()));

                        return new Conversation(this.RestfulClient, null, eventcontext.BaseUri, relatedConversationUri,this);
                    }
                );
                ((IInvitationWithConversation)invite).SetRelatedConversation(relatedConversation);
            }

            //Remove from cache if it is a complete operation
            if (eventcontext.EventEntity.Relationship == EventOperation.Completed)
            {
                IInvitation completedInvite = null;
                Logger.Instance.Information(string.Format("[Communication] Completed and remove invitation: OperationContext:{0}, Href: {1} , LoggingContext: {2}",
                      localResource.OperationContext, NormalizedUri, eventcontext.LoggingContext == null ? string.Empty : eventcontext.LoggingContext.ToString()));
                m_invites.TryRemove(localResource.OperationContext, out completedInvite);
            }

            var eventableEntity = invite as EventableEntity;
            eventableEntity.HandleResourceEvent(eventcontext);

            if (eventcontext.EventEntity.Relationship == EventOperation.Started)
            //here we ignore the case that a new incoming invite is failure and with completed operation
            {
                var temp = eventcontext.EventEntity.EmbeddedResource as InvitationResource;
                if (temp.Direction == Direction.Incoming)
                {
                    var application = this.Parent as Application;
                    var applications = application.Parent as Applications;
                    var discover = applications.Parent as Discover;
                    var endpoint = discover.Parent as ApplicationEndpoint;
                    endpoint.HandleNewIncomingInvite(invite);
                    //TODO:should we treat new incoming INVITE (with new conversation) differently than the incoming modality escalation invite?
                }
            }
        }

        /// <summary>
        /// Handle a invitation complete event
        /// </summary>
        /// <param name="operationId"></param>
        /// <param name="exception"></param>
        internal void HandleInviteStarted(string operationId, IInvitation invite)
        {
            TaskCompletionSource<IInvitation> tcs = null;
            m_inviteAddedTcses.TryGetValue(operationId, out tcs);
            if (tcs != null)
            {
                tcs.TrySetResult(invite);
                TaskCompletionSource<IInvitation> removeTemp = null;
                m_inviteAddedTcses.TryRemove(operationId, out removeTemp);
            }
        }

        /// <summary>
        /// Tracking the invitation resources
        /// </summary>
        /// <param name="operationid"></param>
        /// <param name="tcs"></param>
        internal void HandleNewInviteOperationKickedOff(string operationid, TaskCompletionSource<IInvitation> tcs)
        {
            if (string.IsNullOrEmpty(operationid) || tcs == null)
            {
                throw new RemotePlatformServiceException("Faied to add null object into m_inviteAddedTcses which is to track the incoming invite.");
            }

            m_inviteAddedTcses.TryAdd(operationid, tcs);
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Start messaging
        /// </summary>
        /// <param name="subject"></param>
        /// <param name="to"></param>
        /// <param name="callbackUrl"></param>
        /// <param name="localUserDisplayName"></param>
        /// <param name="localUserUri"></param>
        /// <returns></returns>
        private async Task<IMessagingInvitation> StartMessagingWithIdentityAsync(string subject, string to, string callbackUrl, string href, string localUserDisplayName, string localUserUri, LoggingContext loggingContext = null)
        {
            string operationId = Guid.NewGuid().ToString();
            var tcs = new TaskCompletionSource<IInvitation>();
            HandleNewInviteOperationKickedOff(operationId, tcs);
            IInvitation invite = null;
            var input = new MessagingInvitationInput
            {
                OperationContext = operationId,
                To = to,
                Subject = subject,
                CallbackUrl = callbackUrl,
                LocalUserDisplayName = localUserDisplayName,
                LocalUserUri = localUserUri
            };

            var startMessagingUri = UriHelper.CreateAbsoluteUri(this.BaseUri, href);
            await this.PostRelatedPlatformResourceAsync(startMessagingUri, input, new ResourceJsonMediaTypeFormatter(), loggingContext).ConfigureAwait(false);
            try
            {
                invite = await tcs.Task.TimeoutAfterAsync(WaitForEvents).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                throw new RemotePlatformServiceException("Timeout to get incoming messaging invitation started event from platformservice!");
            }

            //We are sure the invite sure be there now.
            var result = invite as MessagingInvitation;
            if (result == null)
            {
                throw new RemotePlatformServiceException("Platformservice do not deliver a messageInvitation resource with operationId " + operationId);
            }

            return result;
        }

        /// <summary>
        /// Start audio video call
        /// </summary>
        /// <param name="subject"></param>
        /// <param name="to"></param>
        /// <param name="callbackUrl"></param>
        /// <returns></returns>
        private async Task<IAudioVideoInvitation> StartAudioVideoAsync(string href, string subject, string to, string callbackUrl, LoggingContext loggingContext = null)
        {
            var operationId = Guid.NewGuid().ToString();
            var tcs = new TaskCompletionSource<IInvitation>();
            HandleNewInviteOperationKickedOff(operationId, tcs);
            IInvitation invite = null;
            var input = new AudioVideoInvitationInput
            {
                OperationContext = operationId,
                To = to,
                Subject = subject,
                CallbackUrl = callbackUrl,
                MediaHost = MediaHostType.Remote
            };

            Uri startAudioVideoUri = UriHelper.CreateAbsoluteUri(this.BaseUri, href);

            await this.PostRelatedPlatformResourceAsync(startAudioVideoUri, input, new ResourceJsonMediaTypeFormatter(), loggingContext).ConfigureAwait(false);
            try
            {
                invite = await tcs.Task.TimeoutAfterAsync(WaitForEvents).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                throw new RemotePlatformServiceException("Timeout to get incoming audioVideo invitation started event from platformservice!");
            }

            // We are sure that the invite is there now.
            var result = invite as AudioVideoInvitation;
            if (result == null)
            {
                throw new RemotePlatformServiceException("Platformservice do not deliver a AudioVideoInvitation resource with operationId " + operationId);
            }

            return result;
        }

        #endregion
    }
}
