
using Okr_Lambda.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Okr_Lambda.Repository.Interfaces
{
    public interface INotificationsRepository
    {
        MailerTemplate GetMailerTemplate(string templateCode);
        Task<bool> SentMailWithoutAuthenticationAsync(MailRequest mailRequest);
        Task InsertDataInNotificationDetails(NotificationsRequest notificationsRequest);
        Task<IEnumerable<MailerTemplate>> GetTemplate(string templateCode);
    }
}
