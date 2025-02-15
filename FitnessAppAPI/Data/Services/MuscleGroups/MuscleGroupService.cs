﻿using FitnessAppAPI.Common;
using FitnessAppAPI.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace FitnessAppAPI.Data.Services.MuscleGroups
{
    /// <summary>
    ///     Muscle group service class to implement IMuscleGroup interface.
    /// </summary>
    public class MuscleGroupService(FitnessAppAPIContext DB) : BaseService(DB), IMuscleGroupService
    {
        public async Task<ServiceActionResult> GetMuscleGroups(String userId)
        {
            var returnData = await DBAccess.MuscleGroups.Where(m => m.UserId == userId || m.UserId == null)
                                                .OrderBy(m => m.Id)
                                                .Select(m => (BaseModel)ModelMapper.MapToMuscleGroupModel(m))
                                                .ToListAsync();

            if (returnData.Count > 0)
            {
                return new ServiceActionResult(Constants.ResponseCode.SUCCESS, Constants.MSG_SUCCESS, returnData);
            }

            // Should not happen as there are always default muscle groups
            return new ServiceActionResult(Constants.ResponseCode.FAIL, Constants.MSG_NO_MUSCLE_GROUPS_FOUND, returnData);
        }
    }
}
