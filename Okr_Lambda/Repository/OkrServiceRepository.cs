using Okr_Lambda.Models;
using Okr_Lambda.Repository.Interfaces;
using Dapper;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace Okr_Lambda.Repository
{
    public class OkrServiceRepository : BaseRepository, IOkrServiceRepository
    {
        public OkrServiceRepository(IConfiguration configuration, IAdminRepository adminRepository)
   : base(configuration)
        {
        }

        /// <summary>
        /// Get all the KR's of users
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<GoalKey>> GetUsersKRAsync()
        {
            IEnumerable<GoalKey> data = null;
            using (var connection = DbConnectionOkrService)
            {
                if (ConnOkrService != null)
                {

                    data = await connection.QueryAsync<GoalKey>("select EmployeeId  , COUNT(*) count from goalkey where IsActive=1 and DueDate>dateadd(month, +3, getdate()) group by EmployeeId having count(*) >4");
                }
            }

            return data;
        }


        /// <summary>
        /// Get all the Okr's of users where start date should be greater than current date
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<GoalObjective>> GetUsersOkrAsync()
        {
            IEnumerable<GoalObjective> data = null;
            using (var connection = DbConnectionOkrService)
            {
                if (ConnOkrService != null)
                {

                    data = await connection.QueryAsync<GoalObjective>("select EmployeeId  , COUNT(*) count from goalobjective where IsActive=1 and startDate>getdate()  group by EmployeeId having count(*) >4");
                }
            }

            return data;
        }

        public async Task<IEnumerable<GoalObjective>> GetUsersWhoHaveCreatedOkrForNewQuarterAsync()
        {
            IEnumerable<GoalObjective> data = null;
            using (var connection = DbConnectionOkrService)
            {
                if (ConnOkrService != null)
                {

                    data = await connection.QueryAsync<GoalObjective>("select employeeid from GoalObjective where isActive=1 and startDate>getdate() group by employeeid");
                }
            }

            return data;
        }

        /// <summary>
        /// Get all the data from GoalObjective table
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<GoalObjective>> GetAllOkrAsync()
        {
            IEnumerable<GoalObjective> data = null;
            using (var connection = DbConnectionOkrService)
            {
                if (ConnAdmin != null)
                {

                    data = await connection.QueryAsync<GoalObjective>("select * from GoalObjective where IsActive=1");
                }
            }

            return data;

        }

        /// <summary>
        /// Update Goalkey Status to archive 
        /// </summary>
        /// <param name="goalKey"></param>
        /// <returns></returns>
        public async Task<long> UpdateGoalKeyStatus(List<long> goalKey)
        {
            using (var connection = DbConnectionOkrService)
            {
                if (ConnOkrService != null)
                {
                    foreach (var item in goalKey)
                    {
                        string updateQuery = @"UPDATE [DBO].[GoalKey] SET GoalStatusId=3 where goalkeyId =" + item;
                        var result = await connection.ExecuteAsync(updateQuery);

                    }
                }
            }

            return 1;
        }


        /// <summary>
        /// Get all the data from GoalKey table
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<GoalKey>> GetAllKeysAsync()
        {
            IEnumerable<GoalKey> data = null;
            using (var connection = DbConnectionOkrService)
            {
                if (ConnOkrService != null)
                {

                    data = await connection.QueryAsync<GoalKey>("select * from GoalKey where isActive=1");
                }
            }

            return data;

        }

        /// <summary>
        /// Update Objective status to archive
        /// </summary>
        /// <param name="goalObjective"></param>
        /// <returns></returns>
        public async Task<long> UpdateGoalKeyStatus(GoalObjective goalObjective)
        {
            using (var connection = DbConnectionOkrService)
            {
                if (ConnOkrService != null)
                {

                    string updateQuery = @"UPDATE [DBO].[GoalObjective] SET GoalStatusId=3 where GoalObjectiveId =" + goalObjective.GoalObjectiveId;

                    var result = await connection.ExecuteAsync(updateQuery, new
                    {
                        goalObjectiveId = goalObjective.GoalObjectiveId
                    });

                }
            }

            return 1;

        }

        /// <summary>
        /// Getting objective details on the basis of goalObjectiveId
        /// </summary>
        /// <param name="templateCode"></param>
        /// <returns></returns>
        public GoalObjective GetGoalObjectiveById(long goalObjectiveId)
        {
            var data = new GoalObjective();
            using (var connection = DbConnectionOkrService)
            {
                if (ConnOkrService != null)
                {

                    data = connection.QueryFirst<GoalObjective>("select * from GoalObjective where goalObjectiveId = @goalObjectiveId ", new
                    {

                        GoalObjectiveId = goalObjectiveId,
                        IsActive = 1

                    });
                }
            }
            return data;
        }

        /// <summary>
        /// Get all the key against a particular GoalObjectiveId from GoalKey table
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<GoalKey>> GetKeyByGoalObjectiveIdAsync(long goalObjectiveId)
        {
            IEnumerable<GoalKey> data = null;
            using (var connection = DbConnectionOkrService)
            {
                if (ConnOkrService != null)
                {

                    data = await connection.QueryAsync<GoalKey>("select * from GoalKey where isActive=1 and GoalObjectiveId=" + goalObjectiveId);
                }
            }

            return data;

        }

        public async Task<IEnumerable<GoalKey>> GetKeydetailspending(long cycleId)
        {
            IEnumerable<GoalKey> data = null;
            using (var connection = DbConnectionOkrService)
            {
                if (ConnOkrService != null)
                {

                    data = await connection.QueryAsync<GoalKey>("select  * from Goalkey where isActive = 1 and KrstatusId = 1 and CycleId = " + cycleId + " and GoalStatusId != 3 ");
                }
            }

            return data;

        }



    }
}
