using Okr_Lambda.Common;
using Okr_Lambda.Models;
using Okr_Lambda.Repository.Interfaces;
using Dapper;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using MimeKit;
using MailKit.Net.Smtp;
using System.Linq;

namespace Okr_Lambda.Repository
{
    public class NotificationsRepository : BaseRepository, INotificationsRepository
    {
        public IConfiguration Configuration { get; set; }
        public NotificationsRepository(IConfiguration _configuration) : base(_configuration)
        {
        }
        public HttpClient GetHttpClient(string jwtToken)
        {
            HttpClient httpClient = new HttpClient();
            string baseUrl = AppConstants.NotificationUrl;
            httpClient.BaseAddress = new Uri(baseUrl);
            return httpClient;
        }

        /// <summary>
        /// Getting template from Mailertemplate table of Notifications
        /// </summary>
        /// <param name="templateCode"></param>
        /// <returns></returns>
        public MailerTemplate GetMailerTemplate(string templateCode)
        {
            var data = new MailerTemplate();
            using (var connection = DbConnectionNotifications)
            {
                if (ConnNotifications != null)
                {

                    //data = connection.QuerySingle<MailerTemplate>("select * from MailerTemplate where templateCode = " + templateCode + "and isActive=1");

                    data = connection.QueryFirst<MailerTemplate>("select * from MailerTemplate where templateCode = @templateCode ", new
                    {

                        TemplateCode = templateCode,
                        IsActive = 1

                    });
                }
            }
            return data;
        }


        public async Task<IEnumerable<MailerTemplate>> GetTemplate(string templateCode)
        {
            IEnumerable<MailerTemplate> data = null;
            using (var connection = DbConnectionNotifications)
            {
                if (ConnNotifications != null)
                {

                    //data = connection.QuerySingle<MailerTemplate>("select * from MailerTemplate where templateCode = " + templateCode + "and isActive=1");

                    data = await connection.QueryAsync<MailerTemplate>("select * from MailerTemplate where templatecode = " + templateCode + " and isActive = 1");
                }
            }
            return data;
        }

        ///// <summary>
        ///// Sending request with httpclient to notification api
        ///// </summary>
        ///// <param name="mailRequest"></param>
        ///// <param name="jwtToken"></param>
        ///// <returns></returns>
        //public async Task<bool> SentMailWithoutAuthenticationAsync(MailRequest mailRequest, string jwtToken = null)
        //{
        //    HttpClient httpClient = GetHttpClient(jwtToken);
        //    PayloadCustom<bool> payload = new PayloadCustom<bool>();
        //    var response = await httpClient.PostAsJsonAsync($"api/Email/SentMailAsync", mailRequest);
        //    if (response.IsSuccessStatusCode)
        //    {
        //        payload = JsonConvert.DeserializeObject<PayloadCustom<bool>>(await response.Content.ReadAsStringAsync());
        //    }
        //    return payload.IsSuccess;
        //}

        public async Task<bool> SentMailWithoutAuthenticationAsync(MailRequest mailRequest)
        {
            bool IsMailSent = false;
            MailLogRequest log = new MailLogRequest();

            try
            {
                MimeMessage message = new MimeMessage();

                string aWSEmailId = AppConstants.AwsEmailId;
                string account = AppConstants.AccountName; ;
                string password = AppConstants.Password;
                int port = AppConstants.Port;

                string host = AppConstants.Host;
                string environment = "Dev";


                if (string.IsNullOrWhiteSpace(mailRequest.MailFrom) && mailRequest.MailFrom == "")
                {
                    MailboxAddress from = new MailboxAddress("UnlockOKR", aWSEmailId);
                    message.From.Add(from);
                }
                else
                {
                    var isMailExist = IsMailExist(mailRequest.MailFrom);
                    if (isMailExist != null)
                    {
                        MailboxAddress mailboxAddress = new MailboxAddress("User", mailRequest.MailFrom);
                        message.From.Add(mailboxAddress);
                    }
                }

                MailboxAddress From = new MailboxAddress("UnlockOKR", aWSEmailId);
                message.From.Add(From);


                if (environment != "LIVE")
                {
                    mailRequest.Subject = mailRequest.Subject + " - " + environment + " This mail is for " + mailRequest.MailTo;

                    var emails = await GetEmailAddress();
                    foreach (var address in emails)
                    {
                        var emailAddress = new MailboxAddress(address.FullName, address.EmailAddress);
                        message.To.Add(emailAddress);
                    }
                    MailboxAddress CC = new MailboxAddress("alok.parhi@compunneldigital.com");
                    message.Cc.Add(CC);
                }

                else if (environment == "LIVE")
                {
                    string[] strTolist = mailRequest.MailTo.Split(';');

                    foreach (var item in strTolist)
                    {
                        MailboxAddress mailto = new MailboxAddress(item);
                        message.To.Add(mailto);
                    }


                    if (mailRequest.Bcc != "")
                    {
                        string[] strbcclist = mailRequest.CC.Split(';');
                        foreach (var item in strbcclist)
                        {
                            MailboxAddress bcc = new MailboxAddress(item);
                            message.Bcc.Add(bcc);
                        }
                    }

                    if (mailRequest.CC != "")
                    {
                        string[] strCcList = mailRequest.CC.Split(';');
                        foreach (var item in strCcList)
                        {
                            MailboxAddress CC = new MailboxAddress(item);
                            message.Cc.Add(CC);
                        }
                    }
                }


                message.Subject = mailRequest.Subject;
                BodyBuilder bodyBuilder = new BodyBuilder();
                bodyBuilder.HtmlBody = mailRequest.Body;
                message.Body = bodyBuilder.ToMessageBody();


               // await SaveMailAsync(mailRequest);

                if (message.Subject != "")
                {
                    SmtpClient client = new SmtpClient();
                    client.Connect(host, port, false);
                    client.Authenticate(account, password);
                    client.Send(message);
                    client.Disconnect(true);
                    client.Dispose();
                    log.MailTo = mailRequest.MailTo;

                    log.Body = mailRequest.Body;
                    log.MailSubject = mailRequest.Subject;
                    log.CC = mailRequest.CC;
                    log.Bcc = mailRequest.Bcc;
                    log.IsMailSent = true;
                    log.MailSentOn = DateTime.UtcNow;
                    IsMailSent = true;
                    //await MailLogAsync(log);
                }

            }
            catch (Exception e)
            {
                log.MailTo = mailRequest.MailTo;

                log.Body = mailRequest.Body;
                log.MailSubject = mailRequest.Subject;
                log.CC = mailRequest.CC;
                log.Bcc = mailRequest.Bcc;
                log.IsMailSent = false;
                log.MailSentOn = null;
               // await MailLogAsync(log);
                IsMailSent = false;

            }

            return IsMailSent;
        }

        public async Task<MailSetupConfig> IsMailExist(string emailId)
        {

            var data = new MailSetupConfig();
            using (var connection = DbConnectionNotifications)
            {
                if (ConnNotifications != null)
                {

                    //data = connection.QuerySingle<MailerTemplate>("select * from MailerTemplate where templateCode = " + templateCode + "and isActive=1");

                    data = connection.QueryFirst<MailSetupConfig>("select * from MailSetupConfig where AwsemailId = @emailId ", new
                    {

                        TemplateCode = emailId,
                        IsActive = 1

                    });
                }
            }
            return data;

        }

        public async Task<IEnumerable<Emails>> GetEmailAddress()
        {
            IEnumerable<Emails> data = null;
            using (var connection = DbConnectionNotifications)
            {
                if (ConnNotifications != null)
                {

                    data = await connection.QueryAsync<Emails>("select * from Emails");
                }
            }
            return data.ToList();

        }


        /// <summary>
        /// Method that will save notification details in NotificationsDetails table
        /// </summary>
        /// <param name="notificationsRequest"></param>
        /// <returns></returns>
        public async Task InsertDataInNotificationDetails(NotificationsRequest notificationsRequest)
        {
            using (var connection = DbConnectionNotifications)
            {
                if (ConnAdmin != null)
                {
                    string insertQuery = @"INSERT INTO [dbo].[NotificationsDetails] ([NotificationsBy], [NotificationsTo], [NotificationsMessage], [ApplicationMasterId], [IsRead], [IsDeleted],[NotificationTypeId],[MessageTypeId],[Url],[CreatedOn]) VALUES (@By,@To,@Text,@AppId,@IsRead,@IsDeleted,@NotificationType,@MessageType,@Url,@CreatedOn)";

                    var result = await connection.ExecuteAsync(insertQuery, new
                    {
                        notificationsRequest.By,
                        notificationsRequest.To,
                        notificationsRequest.Text,
                        notificationsRequest.AppId,
                        IsRead = 0,
                        IsDeleted = 0,
                        notificationsRequest.NotificationType,
                        notificationsRequest.MessageType,
                        notificationsRequest.Url,
                        CreatedOn = DateTime.Now
                    });

                }
            }

        }
    }
}
