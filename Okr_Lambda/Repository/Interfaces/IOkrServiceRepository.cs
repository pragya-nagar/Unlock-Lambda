using Okr_Lambda.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Okr_Lambda.Repository.Interfaces
{
    public interface IOkrServiceRepository
    {
        Task<IEnumerable<GoalObjective>> GetUsersOkrAsync();
        Task<IEnumerable<GoalKey>> GetUsersKRAsync();
        Task<IEnumerable<GoalObjective>> GetUsersWhoHaveCreatedOkrForNewQuarterAsync();
        Task<IEnumerable<GoalObjective>> GetAllOkrAsync();
        Task<long> UpdateGoalKeyStatus(List<long> goalKey);
        Task<IEnumerable<GoalKey>> GetAllKeysAsync();
        Task<long> UpdateGoalKeyStatus(GoalObjective goalObjective);
        GoalObjective GetGoalObjectiveById(long goalObjectiveId);
        Task<IEnumerable<GoalKey>> GetKeyByGoalObjectiveIdAsync(long goalObjectiveId);
        Task<IEnumerable<GoalKey>> GetKeydetailspending(long cycleId);
    }
}
