using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Okr_Lambda.Common;
using Okr_Lambda.Models;
using Okr_Lambda.Repository.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FluentDateTime;
using Humanizer;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;
using System.Security.Cryptography;
using System.IO;


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Okr_Lambda
{
    public class Function : FunctionBase
    {

        private readonly IAdminRepository _adminDataRepository;
        private readonly IOkrServiceRepository _okrServiceDataRepository;
        private readonly INotificationsRepository _notificationsAndEmails;
        private readonly IConfiguration _configuration;

        public Function() : base()
        {
            _adminDataRepository = _serviceProvider.GetRequiredService<IAdminRepository>();
            _okrServiceDataRepository = _serviceProvider.GetRequiredService<IOkrServiceRepository>();
            _notificationsAndEmails = _serviceProvider.GetRequiredService<INotificationsRepository>();
            _configuration = _serviceProvider.GetRequiredService<IConfiguration>();
        }
        //public async Task<string> FunctionHandler(ILambdaContext context)
        //{
        //    await UpdateSource(context);
        //    /// await SentMailToAllUsers(context);
        //    return "Successful";
        //}

        /// <summary>
        /// Mail to users and manager if user has not logged in within 7 days after addition
        /// Notification to manager
        /// </summary>
        /// <returns></returns>
        public async Task SendMailForLogin(ILambdaContext context)
        {
            List<long> to = new List<long>();
            NotificationsRequest notificationsRequest = new NotificationsRequest();
            var employees = await _adminDataRepository.GetAdminData();
            var userData = employees.ToList();

            var userToken = await _adminDataRepository.GetUserTokenDetails();
            var currentDate = DateTime.UtcNow.AddDays(-7).ToString("dd-MM-yyyy");
            var data = userData.Where(x => x.CreatedOn.ToString("dd-MM-yyyy") == currentDate).ToList();
            if (data.Count > 0)
            {
                foreach (var emp in data)
                {
                    var tokenDetails = userToken.FirstOrDefault(x => x.EmployeeId == emp.EmployeeId && x.LastLoginDate == null && x.CurrentLoginDate == null);
                    var reporting = employees.FirstOrDefault(x => x.EmployeeId == emp.ReportingTo);
                    if (tokenDetails != null)
                    {
                        var template = _notificationsAndEmails.GetMailerTemplate(TemplateCodes.LR.ToString());
                        string body = template.Body.Replace("topBar", AppConstants.CloudFrontUrl + AppConstants.TopBar).Replace("logo", AppConstants.CloudFrontUrl + AppConstants.LogoImage)
                            .Replace("login", AppConstants.CloudFrontUrl + AppConstants.LoginButtonImage).Replace("userManger", AppConstants.CloudFrontUrl + AppConstants.HandShakeImage).Replace("lambdaUrl", AppConstants.ApplicationUrl);
                        string subject = template.Subject;
                        body = body.Replace("managerName", reporting.FirstName).Replace("userName", emp.FirstName);
                        MailRequest mailRequest = new MailRequest();
                        if (emp.EmailId != null && template.Subject != "")
                        {
                            mailRequest.MailTo = emp.EmailId;
                            mailRequest.Subject = subject;
                            mailRequest.Body = body;
                            await _notificationsAndEmails.SentMailWithoutAuthenticationAsync(mailRequest);
                        }

                        var managerTemplate = _notificationsAndEmails.GetMailerTemplate(TemplateCodes.LRM.ToString());
                        string managerTemplatebody = managerTemplate.Body.Replace("topBar", AppConstants.CloudFrontUrl + AppConstants.TopBar).Replace("logo", AppConstants.CloudFrontUrl + AppConstants.LogoImage)
                            .Replace("login", AppConstants.CloudFrontUrl + AppConstants.LoginButtonImage).Replace("userManger", AppConstants.CloudFrontUrl + AppConstants.HandShakeImage).Replace("lambdaUrl", AppConstants.ApplicationUrl);
                        string managerTemplatesubject = managerTemplate.Subject;
                        managerTemplatebody = managerTemplatebody.Replace("managerName", reporting.FirstName).Replace("userName", emp.FirstName);
                        MailRequest mailRequests = new MailRequest();
                        if (reporting.EmailId != null && managerTemplatesubject != "")
                        {
                            mailRequests.MailTo = reporting.EmailId;
                            mailRequests.Subject = managerTemplatesubject;
                            mailRequests.Body = managerTemplatebody;
                            await _notificationsAndEmails.SentMailWithoutAuthenticationAsync(mailRequests);
                        }

                        ///Notification To ReportingManager
                        to.Add(reporting.EmployeeId);
                        notificationsRequest.To = to;
                        notificationsRequest.By = reporting.EmployeeId;
                        notificationsRequest.Url = "";
                        notificationsRequest.Text = AppConstants.ReminderByManagerForUserMessage;
                        notificationsRequest.AppId = Apps.AppId;
                        notificationsRequest.NotificationType = (int)NotificationType.LoginReminderForUser;
                        notificationsRequest.MessageType = (int)MessageTypeForNotifications.NotificationsMessages;
                        await _notificationsAndEmails.InsertDataInNotificationDetails(notificationsRequest);
                    }
                }
            }

        }

        /// <summary>
        /// Update status to archive when planning session closed
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task UpdateStatusAfterPlanningSession(ILambdaContext lambdaContext)
        {
            var organisations = await _adminDataRepository.GetOrganisationsData();

            if (organisations != null)
            {
                foreach (var item in organisations)
                {
                    ///Will fetch active organisationCycle
                    var cycle = await _adminDataRepository.GetOrganisationCycles(item.OrganisationId);
                    foreach (var cycleItem in cycle)
                    {
                        ///will find which cycle is active now
                        bool isCurrentCycle = cycleItem.CycleStartDate <= DateTime.UtcNow && cycleItem.CycleEndDate >= DateTime.UtcNow;
                        if (isCurrentCycle)
                        {
                            var goalUnlockDate = await _adminDataRepository.GetGoalUnlockDateData();
                            var goalLockedDate = goalUnlockDate.Where(x => x.OrganisationCycleId == cycleItem.OrganisationCycleId);

                            DateTime goalSubmitDate = goalLockedDate.Count() != 0 ? goalLockedDate.FirstOrDefault(x => x.Type == AppConstants.SubmitData).SubmitDate : cycleItem.CycleStartDate.AddDays(AppConstants.OkrLockDuration);
                            if (goalSubmitDate <= DateTime.Now)
                            {
                                var keyDetails = await _okrServiceDataRepository.GetAllKeysAsync();
                                if (keyDetails != null)
                                {
                                    var pendingKeys = keyDetails.Where(x => (x.KrStatusId == (int)KrStatus.Pending || x.GoalStatusId == (int)GoalStatus.Draft) && (x.CycleId == cycleItem.OrganisationCycleId)).ToList();

                                    foreach (var key in pendingKeys)
                                    {
                                        if (key.GoalObjectiveId > 0)
                                        {
                                            var okrDetails = await _okrServiceDataRepository.GetAllOkrAsync();
                                            var isGoalExists = okrDetails.FirstOrDefault(x => x.GoalObjectiveId == key.GoalObjectiveId && x.IsActive);
                                            if (isGoalExists != null)
                                            {
                                                var updateObjectiveStatus = await _okrServiceDataRepository.UpdateGoalKeyStatus(isGoalExists);
                                            }

                                        }
                                    }
                                    var keys = pendingKeys.Select(x => x.GoalKeyId).ToList();
                                    var updateStatus = await _okrServiceDataRepository.UpdateGoalKeyStatus(keys);
                                }
                            }

                        }
                    }

                }
            }
        }

        /// <summary>
        /// Email and notification will be send to source after every 7 days in planning session  if his contributor has not done any action like accept / decline on assigned KR
        /// </summary>
        /// <param name="lambdaContext"></param>
        /// <returns></returns>
        public async Task UpdateSource(ILambdaContext context)
        {
            var organisations = await _adminDataRepository.GetOrganisationsData();
            var userDetails = await _adminDataRepository.GetAdminData();

            if (organisations != null)
            {
                foreach (var item in organisations)
                {
                    ///Will fetch active organisationCycle
                    var cycle = await _adminDataRepository.GetOrganisationCycles(item.OrganisationId);
                    foreach (var cycleItem in cycle)
                    {
                        ///will find which cycle is active now
                        bool isCurrentCycle = cycleItem.CycleStartDate <= DateTime.UtcNow && cycleItem.CycleEndDate >= DateTime.UtcNow;
                        if (isCurrentCycle)
                        {
                            ///we are getting the dates on which mail should be send to source in planning session
                            var goalUnlockDate = await _adminDataRepository.GetGoalUnlockDateData();
                            var goalLockedDate = goalUnlockDate.Where(x => x.OrganisationCycleId == cycleItem.OrganisationCycleId);

                            DateTime goalSubmitDate = goalLockedDate.Count() != 0 ? goalLockedDate.FirstOrDefault(x => x.Type == AppConstants.SubmitData).SubmitDate : cycleItem.CycleStartDate.AddDays(AppConstants.OkrLockDuration);

                            var keyDetails = await _okrServiceDataRepository.GetAllKeysAsync();
                            var pendingKeysOfCurrentCycle = keyDetails.Where(x => x.KrStatusId == (int)KrStatus.Pending && x.IsActive && x.CycleId == cycleItem.OrganisationCycleId && x.GoalStatusId != (int)GoalStatus.Archive);
                            if (pendingKeysOfCurrentCycle != null)
                            {
                                var sourceUsers = pendingKeysOfCurrentCycle.GroupBy(x => x.CreatedBy).Select(x => Convert.ToInt64(x.Key)).ToList();
                                foreach (var user in sourceUsers)
                                {
                                    var contributorWithPendingKey = pendingKeysOfCurrentCycle.Where(x => x.CreatedBy == user && x.KrStatusId == (int)KrStatus.Pending && x.IsActive);
                                    if (contributorWithPendingKey != null)
                                    {
                                        Dictionary<long, int> KeyCount = new Dictionary<long, int>();
                                        foreach (var cont in contributorWithPendingKey)
                                        {
                                            ///Adding contributors with pending KR whose createdOn+7 days date match todays date
                                            var date = cont.CreatedOn.AddDays(1);
                                            if (date.ToString("dd-MM-yyyy") == DateTime.Now.ToString("dd-MM-yyyy") && date.ToString("dd-MM-yyyy") != goalSubmitDate.ToString("dd-MM-yyyy"))
                                            {

                                                if (!KeyCount.ContainsKey((long)cont.EmployeeId))
                                                {
                                                    KeyCount.Add((long)cont.EmployeeId, 1);
                                                }
                                                else
                                                {
                                                    KeyCount[(long)cont.EmployeeId]++;
                                                }
                                            }
                                        }

                                        //Source User Details to whom we are sending pending kr details
                                        var userData = userDetails.FirstOrDefault(x => x.EmployeeId == user && x.IsActive);
                                        var template = _notificationsAndEmails.GetMailerTemplate(TemplateCodes.CPS.ToString());
                                        string body = template.Body;
                                        body = body.Replace("topBar", AppConstants.CloudFrontUrl + AppConstants.TopBar).Replace("logo", AppConstants.CloudFrontUrl + AppConstants.LogoImage).Replace("<RedirectOkR>", AppConstants.ApplicationUrl + "?redirectUrl=unlock-me&empId=" + user)
                                            .Replace("<url>", AppConstants.ApplicationUrl).Replace("login", AppConstants.CloudFrontUrl + AppConstants.LoginImage).Replace("name", userData.FirstName).Replace("watch", AppConstants.CloudFrontUrl + AppConstants.Watch).Replace("<dashUrl>", AppConstants.ApplicationUrl + "?redirectUrl=unlock-me&empId=" + user)
                                            .Replace("<RedirectOkR>", AppConstants.ApplicationUrl).Replace("<supportEmailId>", AppConstants.UnlockSupportEmailId).Replace("<unlocklink>", AppConstants.ApplicationUrl).Replace("dot", AppConstants.CloudFrontUrl + AppConstants.DotImage).Replace("yer", Convert.ToString(DateTime.Now.Year))
                                            .Replace("footer", AppConstants.CloudFrontUrl + AppConstants.footer).Replace("<linkedin>", AppConstants.LinkedinLink).Replace("linkedin", AppConstants.CloudFrontUrl + AppConstants.LinkedInImage).Replace("<pri>", AppConstants.PrivacyPolicy).Replace("<tos>", AppConstants.TermsOfUse);

                                        if (KeyCount.Count() > 0)
                                        {
                                            MailRequest mailRequest = new MailRequest();
                                            var summary = string.Empty;
                                            var counter = 0;
                                            foreach (var cont in KeyCount)
                                            {
                                                counter = counter + 1;
                                                ///Contributors details 
                                                var childDetails = userDetails.FirstOrDefault(x => x.EmployeeId == cont.Key && x.IsActive);
                                                summary = summary + "<tr><td valign =\"top\" cellpadding=\"0\" cellspacing=\"0\" style=\"font-size:16px;line-height:24px;color:#292929;font-family: Calibri,Arial;padding-right: 3px;\">" + " " + counter + " " + "." + " </td><td valign =\"top\" cellpadding=\"0\" cellspacing=\"0\" style=\"font-size:16px;line-height:24px;color:#292929;font-family: Calibri,Arial;\">" + " " + childDetails.FirstName + " " + "has" + " " + cont.Value.ToWords() + " " + "pending assignment.</td></tr>";
                                            }
                                            var updatedBody = body;
                                            updatedBody = updatedBody.Replace("<Gist>", summary);
                                            mailRequest.Body = updatedBody;
                                            mailRequest.MailTo = userData.EmailId;
                                            mailRequest.Subject = template.Subject;

                                            await _notificationsAndEmails.SentMailWithoutAuthenticationAsync(mailRequest);
                                        }

                                    }

                                }
                            }


                        }
                    }

                }
            }
        }

        /// <summary>
        /// This method will send an email to all the users who have kr in pending state on the last day of planning session
        /// </summary>
        /// <param name="lambdaContext"></param>
        /// <returns></returns>
        public async Task UsersKrSummary(ILambdaContext lambdaContext)
        {
            var organisations = await _adminDataRepository.GetOrganisationsData();
            var userDetails = await _adminDataRepository.GetAdminData();

            if (organisations != null)
            {
                foreach (var item in organisations)
                {
                    ///Will fetch active organisationCycle
                    var cycle = await _adminDataRepository.GetOrganisationCycles(item.OrganisationId);
                    foreach (var cycleItem in cycle)
                    {
                        ///will find which cycle is active now
                        bool isCurrentCycle = cycleItem.CycleStartDate <= DateTime.UtcNow && cycleItem.CycleEndDate >= DateTime.UtcNow;
                        if (isCurrentCycle)
                        {
                            var goalUnlockDate = await _adminDataRepository.GetGoalUnlockDateData();
                            var goalLockedDate = goalUnlockDate.Where(x => x.OrganisationCycleId == cycleItem.OrganisationCycleId);

                            DateTime goalSubmitDate = goalLockedDate.Count() != 0 ? goalLockedDate.FirstOrDefault(x => x.Type == AppConstants.SubmitData).SubmitDate : cycleItem.CycleStartDate.AddDays(AppConstants.OkrLockDuration);

                            if (goalSubmitDate.ToString("dd-MM-yyyy") == DateTime.Now.ToString("dd-MM-yyyy"))
                            {
                                var keyDetails = await _okrServiceDataRepository.GetAllKeysAsync();
                                var pendingKeysOfCurrentCycle = keyDetails.Where(x => x.KrStatusId == (int)KrStatus.Pending && x.IsActive && x.CycleId == cycleItem.OrganisationCycleId && x.GoalStatusId != (int)GoalStatus.Archive);
                                if (pendingKeysOfCurrentCycle != null)
                                {
                                    var sourceUsers = pendingKeysOfCurrentCycle.GroupBy(x => x.CreatedBy).Select(x => Convert.ToInt64(x.Key)).ToList();
                                    foreach (var user in sourceUsers)
                                    {
                                        var contributorWithPendingKey = pendingKeysOfCurrentCycle.Where(x => x.CreatedBy == user && x.KrStatusId == (int)KrStatus.Pending && x.IsActive).ToList();
                                        if (contributorWithPendingKey != null)
                                        {
                                            var totalCont = contributorWithPendingKey.Count();
                                            //Source User Details to whom we are sending pending kr details
                                            var userData = userDetails.FirstOrDefault(x => x.EmployeeId == user && x.IsActive);
                                            var template = _notificationsAndEmails.GetMailerTemplate(TemplateCodes.LDS.ToString());
                                            string body = template.Body;
                                            body = body.Replace("topBar", AppConstants.CloudFrontUrl + AppConstants.TopBar).Replace("logo", AppConstants.CloudFrontUrl + AppConstants.LogoImage).Replace("infoo", AppConstants.CloudFrontUrl + AppConstants.InfoIcon).Replace("yer", Convert.ToString(DateTime.Now.Year))
                                                .Replace("<url>", AppConstants.ApplicationUrl).Replace("login", AppConstants.CloudFrontUrl + AppConstants.LoginImage).Replace("name", userData.FirstName).Replace("watch", AppConstants.CloudFrontUrl + AppConstants.Watch).Replace("Nume", totalCont.ToString())
                                                .Replace("<RedirectOkR>", AppConstants.ApplicationUrl).Replace("<supportEmailId>", AppConstants.UnlockSupportEmailId).Replace("<unlocklink>", AppConstants.ApplicationUrl).Replace("dot", AppConstants.CloudFrontUrl + AppConstants.DotImage)
                                                .Replace("footer", AppConstants.CloudFrontUrl + AppConstants.footer).Replace("<linkedin>", AppConstants.LinkedinLink).Replace("linkden", AppConstants.CloudFrontUrl + AppConstants.LinkedInImage).Replace("<pri>", AppConstants.PrivacyPolicy).Replace("<tos>", AppConstants.TermsOfUse)
                                                .Replace("<dashUrl>", AppConstants.ApplicationUrl + "?redirectUrl=unlock-me&empId=" + user);

                                            MailRequest mailRequest = new MailRequest();
                                            var summary = string.Empty;
                                            var cycleSymbolId = cycleItem.SymbolId;
                                            var cycleSymbol = _adminDataRepository.GetCycleSymbolById(cycleSymbolId);


                                            var topTwoPendingKeys = contributorWithPendingKey.Take(2);
                                            foreach (var key in topTwoPendingKeys)
                                            {
                                                if (key.GoalObjectiveId > 0)
                                                {
                                                    var objectiveDetails = _okrServiceDataRepository.GetGoalObjectiveById(key.GoalObjectiveId);
                                                    var totalKeys = keyDetails.Where(x => x.GoalObjectiveId == objectiveDetails.GoalObjectiveId).Count();
                                                    summary = summary + "<tr><td cellspacing =\"0\" cellpadding=\"0\"><table width =\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tr><td cellspacing =\"0\" cellpadding=\"0\" style=\"padding-bottom: 10px;\"><table width =\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"background-color: #ffffff;  border-radius: 6px;box-shadow:0px 0px 5px rgba(41, 41, 41, 0.1);\"><tr><td cellspacing =\"0\" cellpadding=\"0\" style=\"padding: 5px;\"><table width =\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tr><td cellspacing =\"0\" cellpadding=\"0\" style=\"padding: 5px 15px;\"><table width =\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tr><td width =\"75%\" cellspacing=\"0\" cellpadding=\"0\" style=\"width:75%\"><table width =\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tr><td cellspacing =\"0\" cellpadding=\"0\" style=\"font-size:16px;line-height:22px;font-weight:400;color:#292929;font-family: Calibri,Arial;padding-bottom: 16px;\">" + " " + objectiveDetails.ObjectiveName + " " + "</td></tr><tr><td cellspacing =\"0\" cellpadding=\"0\"><table width =\"auto\" cellspacing=\"0\" cellpadding=\"0\"><tr><td cellspacing =\"0\" cellpadding=\"0\" valign=\"middle\" align=\"center\" height=\"20\" style=\"color: #ffffff; padding-left: 10px;padding-right:8px;border-radius: 3px;\" bgcolor=\"#39A3FA\"><table width =\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tr><td cellspacing =\"0\" cellpadding=\"0\" valign=\"middle\"><img src =\"" + AppConstants.CloudFrontUrl + AppConstants.RightImage + "\" alt=\"arrow\" style=\"display: block;\"/></td><td cellspacing =\"0\" cellpadding=\"0\" valign=\"middle\" style=\"font-size:12px;line-height:14px;font-weight:bold;color:#ffffff;font-family: Calibri,Arial;padding-left: 6px;\"> " + " " + totalKeys + " " + " Key Results</td></tr></table></td></tr></table></tr></table></td><td cellspacing =\"0\" cellpadding=\"0\" align=\"right\" valign=\"top\"><table width =\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tr><td cellspacing =\"0\" cellpadding=\"0\" align=\"right\" style=\"padding-top: 7px;\" valign=\"top\"><table cellspacing =\"0\" cellpadding=\"0\"> <tr><td cellspacing =\"0\" cellpadding=\"0\" valign=\"top\" style=\"font-size:16px;line-height:18px;font-weight:500;color:#292929;font-family: Calibri,Arial;padding-right: 18px;\">" + CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(objectiveDetails.Enddate.Month) + " " + objectiveDetails.Enddate.Day + "</td><td cellspacing =\"0\" cellpadding=\"0\" valign=\"top\"><img src =\"" + AppConstants.CloudFrontUrl + AppConstants.Calendar + "\" alt=\"cal\" style=\"display: inline-block;\"/></td></tr></table></td></tr><tr><td cellspacing =\"0\" cellpadding=\"0\" align=\"right\" valign= \"top\" style=\"text-align:right;font-size:12px;line-height:12px;font-weight:500;color:#626262;font-family: Calibri,Arial;padding-right: 5px;\">Cycle: " + " " + cycleSymbol.Symbol + " " + ", " + " " + cycleItem.CycleYear + " " + "</td></tr></table></td></tr></table></td></tr></table></td></tr></table></td></tr>";
                                                }
                                                else
                                                {
                                                    summary = summary + "<tr><td cellspacing =\"0\" cellpadding=\"0\" style=\"padding-bottom: 10px;\"><table width =\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"background-color: #ffffff;  border-radius: 6px;box-shadow:0px 0px 5px rgba(41, 41, 41, 0.1);\"><tr><td cellspacing =\"0\" cellpadding=\"0\" style=\"padding: 5px;\"><table width =\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tr><td cellspacing =\"0\" cellpadding=\"0\" bgcolor=\"#F1F3F4\" style=\"padding: 10px 15px;border-radius: 6px;\"><table width =\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tr><td width =\"75%\" cellspacing=\"0\" cellpadding=\"0\" style=\"width:75%\"><table width =\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tr><td cellspacing =\"0\" cellpadding=\"0\" style=\"font-size:16px;line-height:22px;font-weight:400;color:#292929;font-family: Calibri,Arial;padding-bottom: 16px;\">" + " " + key.KeyDescription + " " + "</td></tr><tr><td cellspacing =\"0\" cellpadding=\"0\"><table width =\"auto\" cellspacing=\"0\" cellpadding=\"0\"><tr><td cellspacing =\"0\" cellpadding=\"0\" valign=\"middle\" align=\"left\" width=\"\" height=\"20\" style=\"color: #ffffff;padding-left: 10px;border-radius: 3px;padding-right: 8px;\" bgcolor=\"#e3e5e5\"><table width =\"\" cellspacing=\"0\" cellpadding=\"0\"><tr><td cellspacing =\"0\" cellpadding=\"0\" valign=\"middle\"><img src =\"" + AppConstants.CloudFrontUrl + AppConstants.LinkImage + "\" alt=\"link\" style=\"display: block;\"/></td><td cellspacing =\"0\" cellpadding=\"0\" valign=\"middle\" style=\"font-size:12px;line-height:14px;font-weight:bold;color:#626262;font-family: Calibri,Arial;padding-left: 7px;\">Key Result</td></tr></table></td></tr></table></td></tr></table></td><td cellspacing =\"0\" cellpadding=\"0\" align=\"right\" valign=\"top\"><table width =\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tr><td cellspacing =\"0\" cellpadding=\"0\" align=\"right\" style=\"padding-top: 7px;\" valign=\"top\"><table cellspacing =\"0\" cellpadding=\"0\"><tr><td cellspacing =\"0\" cellpadding=\"0\" valign=\"top\" style=\"font-size:16px;line-height:18px;font-weight:500;color:#292929;font-family: Calibri,Arial;padding-right: 18px;\">" + CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(key.DueDate.Month) + " " + key.DueDate.Day + "</td><td cellspacing =\"0\" cellpadding=\"0\" valign=\"top\"><img src =\"" + AppConstants.CloudFrontUrl + AppConstants.Calendar + "\" alt=\"cal\" style=\"display: inline-block;\"/></td></tr></table></td></tr><tr><td cellspacing =\"0\" cellpadding=\"0\" align=\"right\" valign=\"top\" style=\"text-align:right;font-size:12px;line-height:12px;font-weight:500;color:#626262;font-family: Calibri,Arial;padding-right: 5px;\">Cycle: " + " " + cycleSymbol.Symbol + " " + ", " + "" + cycleItem.CycleYear + " " + "</td></tr></table></td></tr></table></td></tr></table></td></tr></table></td></tr>";
                                                }
                                            }


                                            var updatedBody = body;
                                            updatedBody = updatedBody.Replace("<Gist>", summary);
                                            mailRequest.Body = updatedBody;
                                            mailRequest.MailTo = userData.EmailId;
                                            mailRequest.Subject = template.Subject;

                                            await _notificationsAndEmails.SentMailWithoutAuthenticationAsync(mailRequest);

                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// This method will send an email and notifications to all the users who have Okr in Draft state on the last day of planning session
        /// </summary>
        /// <param name="lambdaContext"></param>
        /// <returns></returns>
        public async Task UsersDraftKrSummary(ILambdaContext lambdaContext)
        {
            var organisations = await _adminDataRepository.GetOrganisationsData();
            var userDetails = await _adminDataRepository.GetAdminData();

            if (organisations != null)
            {
                foreach (var item in organisations)
                {
                    ///Will fetch active organisationCycle
                    var cycle = await _adminDataRepository.GetOrganisationCycles(item.OrganisationId);
                    foreach (var cycleItem in cycle)
                    {
                        ///will find which cycle is active now
                        bool isCurrentCycle = cycleItem.CycleStartDate <= DateTime.UtcNow && cycleItem.CycleEndDate >= DateTime.UtcNow;
                        if (isCurrentCycle)
                        {
                            var goalUnlockDate = await _adminDataRepository.GetGoalUnlockDateData();
                            var goalLockedDate = goalUnlockDate.Where(x => x.OrganisationCycleId == cycleItem.OrganisationCycleId);

                            DateTime goalSubmitDate = goalLockedDate.Count() != 0 ? goalLockedDate.FirstOrDefault(x => x.Type == AppConstants.SubmitData).SubmitDate : cycleItem.CycleStartDate.AddDays(AppConstants.OkrLockDuration);
                            var reminderDate = goalSubmitDate.AddDays(-1);

                            if (reminderDate.ToString("dd-MM-yyyy") == DateTime.Now.ToString("dd-MM-yyyy"))
                            {
                                var goalDetails = await _okrServiceDataRepository.GetAllOkrAsync();
                                if (goalDetails != null)
                                {
                                    var draftOkrOfCurrentCycle = goalDetails.Where(x => x.IsActive && x.GoalStatusId == (int)GoalStatus.Draft && x.ObjectiveCycleId == cycleItem.OrganisationCycleId).ToList();
                                    if (draftOkrOfCurrentCycle.Count > 0 && draftOkrOfCurrentCycle.Any())
                                    {
                                        var sourceUsers = draftOkrOfCurrentCycle.GroupBy(x => x.CreatedBy).Select(x => Convert.ToInt64(x.Key)).ToList();
                                        foreach (var user in sourceUsers)
                                        {
                                            var sourceWithDraftOkr = draftOkrOfCurrentCycle.Where(x => x.CreatedBy == user).ToList();

                                            if (sourceWithDraftOkr.Count > 0 && sourceWithDraftOkr.Any())
                                            {
                                                var summary = string.Empty;
                                                var count = string.Empty;
                                                var cycleSymbolDetails = _adminDataRepository.GetCycleSymbolById(cycleItem.SymbolId);
                                                var OkrList = sourceWithDraftOkr.Take(3);
                                                foreach (var draftOkr in OkrList)
                                                {
                                                    var keyDetails = await _okrServiceDataRepository.GetKeyByGoalObjectiveIdAsync(draftOkr.GoalObjectiveId);
                                                    var keyCount = keyDetails.Count();
                                                    if (keyCount <= 9)
                                                    {
                                                        count = "0" + Convert.ToString(keyCount);
                                                    }
                                                    else
                                                    {
                                                        count = Convert.ToString(keyCount);
                                                    }

                                                    var stringLen = draftOkr.ObjectiveName.Length;
                                                    if (stringLen > 117)
                                                    {
                                                        draftOkr.ObjectiveName = draftOkr.ObjectiveName.Substring(0, 117) + "...";
                                                    }

                                                    summary = summary + "<tr><td cellspacing =\"0\" cellpadding=\"0\" style=\"padding-bottom: 10px;\"><table width =\"100%\" cellspacing=\"0\" cellpadding=\"0\"style =\"background-color: #ffffff;  border-radius: 6px;box-shadow:0px 0px 5px rgba(41, 41, 41, 0.1);\"><tr><td cellspacing =\"0\" cellpadding=\"0\" style=\"padding: 5px;\"><table width =\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tr><td cellspacing =\"0\" cellpadding=\"0\"style =\"padding: 5px 15px;\"><table width =\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tr><td width =\"75%\" cellspacing=\"0\" cellpadding=\"0\"style =\"width:75%\"><table width =\"100%\" cellspacing=\"0\"cellpadding =\"0\"><tr><td cellspacing =\"0\" cellpadding=\"0\"style =\"font-size:16px;line-height:22px;font-weight:400;color:#292929;font-family: Calibri,Arial;padding-bottom: 16px;\">" + draftOkr.ObjectiveName + "</td></tr><tr><td cellspacing =\"0\" cellpadding=\"0\"><table width =\"auto\"cellspacing =\"0\"cellpadding =\"0\"><tr><td cellspacing =\"0\"cellpadding =\"0\"valign =\"middle\"align =\"center\"height =\"20\"style =\"color: #ffffff; padding-left: 10px;padding-right:8px;border-radius: 3px;\"bgcolor =\"#39A3FA\"><table width =\"100%\"cellspacing =\"0\"cellpadding =\"0\"><tr><td cellspacing =\"0\"cellpadding =\"0\"valign =\"middle\"><img src =\"" + AppConstants.CloudFrontUrl + AppConstants.RightImage + "\"alt =\"arrow\"style =\"display: block;\" /></td><td cellspacing =\"0\"cellpadding =\"0\"valign =\"middle\"style =\"font-size:12px;line-height:14px;font-weight:bold;color:#ffffff;font-family: Calibri,Arial;padding-left: 6px;\">" + count + " Key Results</td></tr></table></td></tr></table></tr></table></td><td cellspacing =\"0\" cellpadding=\"0\"align =\"right\" valign=\"top\"><table width =\"100%\" cellspacing=\"0\"cellpadding =\"0\"><tr><td cellspacing =\"0\" cellpadding=\"0\"align =\"right\"style =\"padding-top: 7px;\"valign =\"top\"><table cellspacing =\"0\"cellpadding =\"0\"><tr><td cellspacing =\"0\"cellpadding =\"0\"valign =\"top\"style =\"font-size:16px;line-height:18px;font-weight:500;color:#292929;font-family: Calibri,Arial;padding-right: 18px;\">" + CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(draftOkr.Enddate.Month) + " " + draftOkr.Enddate.Day + "</td><td cellspacing =\"0\"cellpadding =\"0\"valign =\"top\"><img src =\"" + AppConstants.CloudFrontUrl + AppConstants.Calendar + "\"alt =\"cal\"style =\"display: inline-block;\" /></td></tr></table></td></tr><tr><td cellspacing =\"0\" cellpadding=\"0\"align =\"right\" valign=\"top\" style =\"text-align:right;font-size:12px;line-height:12px;font-weight:500;color:#626262;font-family: Calibri,Arial;padding-right: 5px;\">Cycle: " + cycleSymbolDetails.Symbol + ", " + cycleItem.CycleYear + "</td></tr></table></td></tr></table></td></tr></table></td></tr></table></td></tr> ";

                                                }

                                                var template = _notificationsAndEmails.GetMailerTemplate(TemplateCodes.DOS.ToString());
                                                string body = template.Body;
                                                var subject = template.Subject;
                                                var userData = userDetails.FirstOrDefault(x => x.EmployeeId == user && x.IsActive);
                                                var loginUrl = AppConstants.ApplicationUrl;
                                                if (!string.IsNullOrEmpty(loginUrl))
                                                {
                                                    loginUrl = loginUrl + "?redirectUrl=unlock-me&empId=" + user;
                                                }

                                                body = body.Replace("topBar", AppConstants.CloudFrontUrl + AppConstants.TopBar).Replace("<URL>", loginUrl).Replace("logo", AppConstants.CloudFrontUrl + AppConstants.LogoImage)
                                                    .Replace("loggedInButton", AppConstants.CloudFrontUrl + AppConstants.LoginButtonImage).Replace("name", userData.FirstName).Replace("infoIcon", AppConstants.CloudFrontUrl + AppConstants.InfoIcon)
                                                    .Replace("count", Convert.ToString(sourceWithDraftOkr.Count)).Replace("Listing", summary).Replace("<Button>", loginUrl).Replace("supportEmailId", AppConstants.UnlockSupportEmailId)
                                                    .Replace("footer", AppConstants.CloudFrontUrl + AppConstants.footer).Replace("linkden", AppConstants.CloudFrontUrl + AppConstants.LinkedInImage).Replace("privacy", AppConstants.PrivacyPolicy)
                                                    .Replace("dot", AppConstants.CloudFrontUrl + AppConstants.DotImage).Replace("terming", AppConstants.TermsOfUse).Replace("year", Convert.ToString(DateTime.Now.Year));

                                                subject = subject.Replace("<username>", userData.FirstName);

                                                if (userData.EmailId != null && template.Subject != "")
                                                {
                                                    var mailRequest = new MailRequest
                                                    {
                                                        MailTo = userData.EmailId,
                                                        Subject = subject,
                                                        Body = body
                                                    };
                                                    await _notificationsAndEmails.SentMailWithoutAuthenticationAsync(mailRequest);
                                                }

                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Mail to users after a span of every 3 working days if users have not picked up draft OKRs in the planning session
        /// </summary>
        /// <returns></returns>
        public async Task SendInterimMailForDraftOkr(ILambdaContext context)
        {
            var organisations = await _adminDataRepository.GetOrganisationsData();
            var userDetails = await _adminDataRepository.GetAdminData();

            if (organisations != null)
            {
                foreach (var item in organisations)
                {
                    ///Will fetch active organisationCycle
                    var cycle = await _adminDataRepository.GetOrganisationCycles(item.OrganisationId);
                    foreach (var cycleItem in cycle)
                    {
                        ///will find which cycle is active now
                        bool isCurrentCycle = cycleItem.CycleStartDate <= DateTime.UtcNow && cycleItem.CycleEndDate >= DateTime.UtcNow;
                        if (isCurrentCycle)
                        {
                            var goalDetails = await _okrServiceDataRepository.GetAllOkrAsync();
                            if (goalDetails != null)
                            {
                                var draftOkrOfCurrentCycle = goalDetails.Where(x => x.IsActive && x.GoalStatusId == (int)GoalStatus.Draft && x.ObjectiveCycleId == cycleItem.OrganisationCycleId).ToList();

                                if (draftOkrOfCurrentCycle.Count > 0 && draftOkrOfCurrentCycle.Any())
                                {
                                    var sourceUsers = draftOkrOfCurrentCycle.GroupBy(x => x.CreatedBy).Select(x => Convert.ToInt64(x.Key)).ToList();
                                    foreach (var user in sourceUsers)
                                    {
                                        var sourceWithDraftOkr = draftOkrOfCurrentCycle.Where(x => x.CreatedBy == user).ToList();

                                        if (sourceWithDraftOkr.Count > 0 && sourceWithDraftOkr.Any())
                                        {
                                            var template = _notificationsAndEmails.GetMailerTemplate(TemplateCodes.DIM.ToString());
                                            var body = template.Body;
                                            var subject = template.Subject;
                                            var userData = userDetails.FirstOrDefault(x => x.EmployeeId == user && x.IsActive);
                                            var sourceOldestDraftOkr = sourceWithDraftOkr.OrderBy(x => x.CreatedOn).FirstOrDefault();

                                            var loginUrl = AppConstants.ApplicationUrl;
                                            if (!string.IsNullOrEmpty(loginUrl))
                                            {
                                                loginUrl = loginUrl + "?redirectUrl=unlock-me&empId=" + user;
                                            }

                                            body = body.Replace("topBar", AppConstants.CloudFrontUrl + AppConstants.TopBar).Replace("<URL>", loginUrl).Replace("logo", AppConstants.CloudFrontUrl + AppConstants.LogoImage)
                                                .Replace("loggedInButton", AppConstants.CloudFrontUrl + AppConstants.LoginButtonImage).Replace("name", userData.FirstName).Replace("<Button>", AppConstants.ApplicationUrl + "?redirectUrl=KRAcceptDecline/1/" + sourceOldestDraftOkr.GoalObjectiveId + "&empId=" + user)
                                                .Replace("messageInterm", AppConstants.CloudFrontUrl + AppConstants.MessageIntermImage).Replace("supportEmailId", AppConstants.UnlockSupportEmailId).Replace("footer", AppConstants.CloudFrontUrl + AppConstants.footer)
                                                .Replace("linkden", AppConstants.CloudFrontUrl + AppConstants.LinkedInImage).Replace("privacy", AppConstants.PrivacyPolicy).Replace("dot", AppConstants.CloudFrontUrl + AppConstants.DotImage)
                                                .Replace("terming", AppConstants.TermsOfUse).Replace("year", Convert.ToString(DateTime.Now.Year));

                                            subject = subject.Replace("<username>", userData.FirstName);

                                            ///we are getting the dates on which mail should be send to source in planning session
                                            var dates = new List<DateTime>();
                                            var date = new DateTime();
                                            date = sourceOldestDraftOkr.CreatedOn.AddBusinessDays(3);

                                            var goalUnlockDate = await _adminDataRepository.GetGoalUnlockDateData();
                                            var goalLockedDate = goalUnlockDate.Where(x => x.OrganisationCycleId == cycleItem.OrganisationCycleId);
                                            var goalSubmitDate = goalLockedDate.Count() != 0 ? goalLockedDate.FirstOrDefault(x => x.Type == AppConstants.SubmitData).SubmitDate : cycleItem.CycleStartDate.AddDays(AppConstants.OkrLockDuration);

                                            do
                                            {
                                                dates.Add(date);
                                                date = date.AddBusinessDays(3);
                                            } while (date <= goalSubmitDate);

                                            foreach (var day in dates)
                                            {
                                                if (day.ToString("dd-MM-yyyy") == DateTime.Now.ToString("dd-MM-yyyy"))
                                                {
                                                    if (userData.EmailId != null && template.Subject != "")
                                                    {
                                                        var mailRequest = new MailRequest
                                                        {
                                                            MailTo = userData.EmailId,
                                                            Subject = subject,
                                                            Body = body
                                                        };
                                                        await _notificationsAndEmails.SentMailWithoutAuthenticationAsync(mailRequest);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public async Task SourceAfter3days(ILambdaContext context)
        {
            var organisations = await _adminDataRepository.GetOrganisationsData();
            var userDetails = await _adminDataRepository.GetAdminData();
            if (organisations != null)
            {
                foreach (var item in organisations)
                {
                    var cycle = await _adminDataRepository.GetOrganisationCycles(item.OrganisationId);
                    foreach (var cycleItem in cycle)
                    {
                        bool isCurrentCycle = cycleItem.CycleStartDate <= DateTime.UtcNow && cycleItem.CycleEndDate >= DateTime.UtcNow;
                        if (isCurrentCycle)
                        {

                            var goalUnlockDate = await _adminDataRepository.GetGoalUnlockDateData();
                            var goalLockedDate = goalUnlockDate.Where(x => x.OrganisationCycleId == cycleItem.OrganisationCycleId);

                            DateTime goalSubmitDate = goalLockedDate.Count() != 0 ? goalLockedDate.FirstOrDefault(x => x.Type == AppConstants.SubmitData).SubmitDate : cycleItem.CycleStartDate.AddDays(AppConstants.OkrLockDuration);

                            if (goalSubmitDate.ToString("dd-MM-yyyy") != DateTime.Now.ToString("dd-MM-yyyy"))
                            {
                                var pendingKeysOfCurrentCycle = await _okrServiceDataRepository.GetKeydetailspending(cycleItem.OrganisationCycleId);
                                var pendingkeysforBusinessDay = pendingKeysOfCurrentCycle.ToList().Where(x => Convert.ToDateTime(x.CreatedOn.AddBusinessDays(3)).ToString("dd-MM-yyyy") == DateTime.Now.ToString("dd-MM-yyyy"));
                                var sourceUsers = pendingkeysforBusinessDay.GroupBy(x => x.CreatedBy).Select(x => Convert.ToInt64(x.Key)).ToList();
                                if (sourceUsers.Count > 0)
                                {
                                    foreach (var user in sourceUsers)
                                    {
                                        var contributorWithPendingKey = pendingKeysOfCurrentCycle.Where(x => x.CreatedBy == user && x.KrStatusId == (int)KrStatus.Pending && x.IsActive);
                                        if (contributorWithPendingKey != null)
                                        {
                                            var userData = userDetails.FirstOrDefault(x => x.EmployeeId == user && x.IsActive);
                                            var template = _notificationsAndEmails.GetMailerTemplate(TemplateCodes.CPS.ToString());
                                            string body = template.Body;
                                            body = body.Replace("topBar", AppConstants.CloudFrontUrl + AppConstants.TopBar).Replace("logo", AppConstants.CloudFrontUrl + AppConstants.LogoImage)
                                                .Replace("<url>", AppConstants.ApplicationUrl).Replace("login", AppConstants.CloudFrontUrl + AppConstants.LoginImage).Replace("name", userData.FirstName).Replace("watch", AppConstants.CloudFrontUrl + AppConstants.Watch)
                                                .Replace("<RedirectOkR>", AppConstants.ApplicationUrl).Replace("<supportEmailId>", AppConstants.UnlockSupportEmailId).Replace("<unlocklink>", AppConstants.ApplicationUrl).Replace("dot", AppConstants.CloudFrontUrl + AppConstants.DotImage)
                                                .Replace("footer", AppConstants.CloudFrontUrl + AppConstants.footer).Replace("<linkedin>", AppConstants.LinkedinLink).Replace("linkedin", AppConstants.CloudFrontUrl + AppConstants.LinkedInImage).Replace("<pri>", AppConstants.PrivacyPolicy).Replace("<tos>", AppConstants.TermsOfUse);

                                            Dictionary<long, int> KeyCount = new Dictionary<long, int>();
                                            foreach (var contKey in contributorWithPendingKey)
                                            {
                                                var contributorDetails = contributorWithPendingKey.FirstOrDefault(x => x.EmployeeId == contKey.EmployeeId && x.IsActive);

                                                if (!KeyCount.ContainsKey((long)contributorDetails.EmployeeId))
                                                {
                                                    KeyCount.Add((long)contributorDetails.EmployeeId, 1);
                                                }

                                            }


                                            MailRequest mailRequest = new MailRequest();
                                            var summary = string.Empty;
                                            foreach (var cont in KeyCount)
                                            {
                                                ///Contributors details 
                                                var childDetails = userDetails.FirstOrDefault(x => x.EmployeeId == cont.Key && x.IsActive);
                                                summary = summary + "<tr><td valign =\"top\" cellpadding=\"0\" cellspacing=\"0\" style=\"font-size:16px;line-height:24px;color:#292929;font-family: Calibri,Arial;padding-right: 3px;\">1.</td><td valign =\"top\" cellpadding=\"0\" cellspacing=\"0\" style=\"font-size:16px;line-height:24px;color:#292929;font-family: Calibri,Arial;\">" + " " + childDetails.FirstName + " " + "has " + " " + cont.Value.ToWords() + " " + "  pending assignment</td></tr>";
                                            }
                                            var updatedBody = body;
                                            updatedBody = updatedBody.Replace("<Gist>", summary);
                                            mailRequest.Body = updatedBody;
                                            mailRequest.MailTo = userData.EmailId;
                                            mailRequest.Subject = template.Subject;

                                            await _notificationsAndEmails.SentMailWithoutAuthenticationAsync(mailRequest);

                                            MailRequest mailRequests = new MailRequest();
                                            var Contributorstemplate = _notificationsAndEmails.GetMailerTemplate(TemplateCodes.PC.ToString());
                                            string detail = string.Empty;
                                            string mailId = string.Empty;


                                            string Contributorsbody = Contributorstemplate.Body;
                                            Contributorsbody = Contributorsbody.Replace("topBar", AppConstants.CloudFrontUrl + AppConstants.TopBar).Replace("logo", AppConstants.CloudFrontUrl + AppConstants.LogoImage)
                                                .Replace("<URL>", AppConstants.ApplicationUrl).Replace("login", AppConstants.CloudFrontUrl + AppConstants.LoginImage).Replace("name", userData.FirstName).Replace("assignments", AppConstants.CloudFrontUrl + AppConstants.assignments)
                                                .Replace("<RedirectOkR>", AppConstants.ApplicationUrl).Replace("supportEmailId", AppConstants.UnlockSupportEmailId).Replace("<unlocklink>", AppConstants.ApplicationUrl).Replace("dot", AppConstants.CloudFrontUrl + AppConstants.DotImage)
                                                .Replace("footer", AppConstants.CloudFrontUrl + AppConstants.footer).Replace("linkden", AppConstants.CloudFrontUrl + AppConstants.LinkedInImage).Replace("policy", AppConstants.PrivacyPolicy).Replace("terming", AppConstants.TermsOfUse);
                                            var Contributorsummary = string.Empty;

                                            foreach (var con in contributorWithPendingKey)
                                            {
                                                detail = userDetails.FirstOrDefault(x => x.EmployeeId == con.EmployeeId && x.IsActive).FirstName;
                                                mailId = userDetails.FirstOrDefault(x => x.EmployeeId == con.EmployeeId && x.IsActive).EmailId;
                                                var details = contributorWithPendingKey.FirstOrDefault(x => x.EmployeeId == con.EmployeeId && x.IsActive);
                                                Contributorsummary = "<td valign =\"top\" cellpadding=\"0\" cellspacing =\"0\"style =\"font-size:16px;line-height:24px;color:#292929;font-family: Calibri,Arial;padding-right: 3px\"> </td><td valign =\"top\" cellpadding=\"0\" cellspacing =\"0\" style =\"font-size:16px;line-height:24px;color:#39A3FA;font-family: Calibri,Arial;font-weight: bold;text-decoration: none;\">OKR/KR</a><strong  style =\"font-size:16px;line-height:24px;color:#292929;font-family: Calibri,Arial;\"> <a href =\"#\"  style =\"color: #39A3FA;\">from " + userData.FirstName + "</td></tr>";
                                                var updatedContributors = Contributorsbody;
                                                updatedContributors = updatedContributors.Replace("<subordinate>", Contributorsummary).Replace("Contri", detail);
                                                mailRequests.Body = updatedContributors;
                                                mailRequests.MailTo = mailId;
                                                mailRequests.Subject = Contributorstemplate.Subject;

                                                await _notificationsAndEmails.SentMailWithoutAuthenticationAsync(mailRequests);

                                            }


                                        }
                                    }

                                }

                            }



                        }


                    }

                }
            }
        }

        public async Task<List<PassportEmployeeResponse>> ActiveUserAsync(ILambdaContext context)
        {
            var passportUsersList = new List<PassportEmployeeResponse>();
            var passportUsers = await GetAllPassportUsersAsync();
            if (passportUsers != null && passportUsers.Count > 0)
            {
                passportUsers = passportUsers.Where(x => x.IsActive).ToList();
                foreach (var user in passportUsers)
                {
                    if (user.OrganizationName == AppConstants.Learning || user.OrganizationName == AppConstants.UnlockLearn || user.OrganizationName == AppConstants.InfoProLearning || user.OrganizationName == AppConstants.Unlocklearn)
                    {
                        user.OrganizationName = AppConstants.InfoproLearning;
                    }
                    else if (user.OrganizationName == AppConstants.Digital)
                    {
                        user.OrganizationName = AppConstants.CompunnelDigital;
                    }
                    else if (user.OrganizationName == AppConstants.Staffing)
                    {
                        user.OrganizationName = AppConstants.CompunnelStaffing;
                    }
                    else
                    {
                        user.OrganizationName = "Infopro Learning Inc.";
                    }
                    var employee =  _adminDataRepository.GetEmployeeDetails(Convert.ToString(user.EmployeeId));
                    var orgDetail = _adminDataRepository.GetOrganisationDetails(user.OrganizationName);
                    var employeeByEmailId = _adminDataRepository.GetEmailDetails(user.MailId);
                    if (employee != null && orgDetail != null)
                    {
                        var reportingUser = _adminDataRepository.GetReportingTo(Convert.ToString(user.ReportingTo));
                        employee.FirstName = user.FirstName;
                        employee.LastName = user.LastName;
                        employee.Designation = user.DesignationName;
                        if (reportingUser != null)
                        {
                            employee.ReportingTo = reportingUser.EmployeeId;
                        }
                        employee.IsActive = user.IsActive;
                        employee.UpdatedOn = DateTime.UtcNow;
                        employee.EmailId = user.MailId;
                        await _adminDataRepository.UpdateEmployee(employee.FirstName, employee.LastName, employee.EmailId, employee.Designation, (long)employee.ReportingTo, employee.IsActive, employee.EmployeeId);
                        passportUsersList.Add(user);
                    }
                    else if (employee == null && orgDetail != null && employeeByEmailId == null)
                    {
                        var roleDetails = _adminDataRepository.GetRoleName();
                        var reportingUser = _adminDataRepository.GetReportingTo(Convert.ToString(user.ReportingTo));
                        string salt = Guid.NewGuid().ToString();
                        Employees employees = new Employees();
                        employees.EmployeeCode = Convert.ToString(user.EmployeeId);
                        employees.FirstName = user.FirstName;
                        employees.LastName = user.LastName;
                        employees.Password = EncryptRijndael("abcd@1234", salt);
                        employees.PasswordSalt = salt;
                        employees.Designation = user.DesignationName;
                        employees.EmailId = user.MailId;
                        if (reportingUser != null)
                        {
                            employees.ReportingTo = reportingUser.EmployeeId;
                        }
                        employees.OrganisationId = orgDetail.OrganisationId;
                        employees.IsActive = user.IsActive;
                        employees.CreatedBy = 0;
                        employees.CreatedOn = DateTime.UtcNow;
                        employees.RoleId = roleDetails.RoleId;
                        employees.LoginFailCount = 0;
                       await _adminDataRepository.InsertEmployees(employees.EmployeeCode, employees.FirstName, employees.LastName, employees.EmailId, employees.Designation,(long) employees.ReportingTo, employees.Password, employees.PasswordSalt, employees.OrganisationId, employees.IsActive, employees.RoleId);
                        passportUsersList.Add(user);
                    }
                }
            }
            return passportUsersList;
        }


        public async Task<List<PassportEmployeeResponse>> InActiveUserAsync(ILambdaContext context)
        {
            var passportUsersList = new List<PassportEmployeeResponse>();
            try
            {
               
                var passportUsers = await GetAllPassportUsersAsync();
                if (passportUsers != null && passportUsers.Count > 0)
                {
                    passportUsers = passportUsers.Where(x => !x.IsActive).ToList();
                    foreach (var user in passportUsers)
                    {
                        var employee = _adminDataRepository.GetEmployeeDetails(Convert.ToString(user.EmployeeId));
                        if (employee != null)
                        {
                            employee.IsActive = user.IsActive;
                            employee.UpdatedOn = DateTime.UtcNow;
                            await _adminDataRepository.UpdateInactiveEmployee(employee.IsActive, employee.EmployeeId);
                            passportUsersList.Add(user);

                            var getActiveToken = await _adminDataRepository.GetToken(employee.EmployeeId);
                            if (getActiveToken != null)
                            {
                                getActiveToken.ExpireTime = DateTime.UtcNow.AddMinutes(-1);
                                getActiveToken.LastLoginDate = (DateTime)getActiveToken.CurrentLoginDate;

                                DateTime? date = getActiveToken.LastLoginDate;
                                string sqlFormattedDate = date.Value.ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture);

                                DateTime dt1 = getActiveToken.ExpireTime;
                                string sqlFormattedDate1 = dt1.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                                await _adminDataRepository.UpdateToken(sqlFormattedDate1,sqlFormattedDate, getActiveToken.EmployeeId);


                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                var msg = e.Message;
            }

            return passportUsersList;
        }



        public async Task<List<PassportEmployeeResponse>> GetAllPassportUsersAsync()
        {
            var userType = AppConstants.PassportUserType;
            List<PassportEmployeeResponse> loginUserDetail = new List<PassportEmployeeResponse>();
            HttpClient httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(AppConstants.PassportBaseAddress);
            var response = await httpClient.GetAsync($"User?userType=" + userType);

            if (response.IsSuccessStatusCode)
            {
                var payload = JsonConvert.DeserializeObject<PayloadCustomPassport<PassportEmployeeResponse>>(await response.Content.ReadAsStringAsync());
                loginUserDetail = payload.EntityList;
            }
            return loginUserDetail;
        }

        public string EncryptRijndael(string input, string salt)
        {
            if (string.IsNullOrEmpty(input))
                throw new ArgumentNullException("input");

            var aesAlg = NewRijndaelManaged(salt);

            var encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);
            var msEncrypt = new MemoryStream();
            using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
            using (var swEncrypt = new StreamWriter(csEncrypt))
            {
                swEncrypt.Write(input);
            }

            return Convert.ToBase64String(msEncrypt.ToArray());
        }

        private static RijndaelManaged NewRijndaelManaged(string salt)
        {
            string InputKey = "99334E81-342C-4900-86D9-07B7B9FE5EBB";
            if (salt == null) throw new ArgumentNullException("salt");
            var saltBytes = Encoding.ASCII.GetBytes(salt);
            var key = new Rfc2898DeriveBytes(InputKey, saltBytes);

            var aesAlg = new RijndaelManaged();
            aesAlg.Key = key.GetBytes(aesAlg.KeySize / 8);
            aesAlg.IV = key.GetBytes(aesAlg.BlockSize / 8);

            return aesAlg;
        }


    }
}

