﻿using FitnessAppAPI.Common;
using FitnessAppAPI.Data.Models;
using FitnessAppAPI.Data.Services.User.Models;
using FitnessAppAPI.Data.Services.UserProfile.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Net;

namespace FitnessAppAPI.Data.Services.UserProfile
{
    public class UserProfileService(FitnessAppAPIContext DB): BaseService(DB), IUserProfileService
    {
        public async Task<ServiceActionResult<BaseModel>> AddUserDefaultValues(string userId)
        {
            var kg = await DBAccess.WeightUnits.Where(w => w.Text == Constants.DBConstants.KG).FirstOrDefaultAsync();
            if (kg == null)
            {
                // Must NOT happen
                return new ServiceActionResult<BaseModel>(HttpStatusCode.InternalServerError, Constants.MSG_UNEXPECTED_DB_ERROR);
            }

            // Create ExerciseDefaultValue record for the user
            var defaultValues = new UserDefaultValue
            {
                Sets = 0,
                Reps = 0,
                Weight = 0,
                Rest = 0,
                WeightUnitId = kg.Id,
                Completed = false,
                UserId = userId,
                MGExeciseId = 0,
            };

            DBAccess.UserDefaultValues.Add(defaultValues);
            DBAccess.SaveChanges();

            return new ServiceActionResult<BaseModel>(HttpStatusCode.Created);
        }

        public async Task<ServiceActionResult<UserDefaultValuesModel>> UpdateUserDefaultValues(Dictionary<string, string> requestData, string userId)
        {
            /// Check if new pass is provided
            if (!requestData.TryGetValue("values", out string? serializedValues))
            {
                return new ServiceActionResult<UserDefaultValuesModel>(HttpStatusCode.BadRequest, Constants.MSG_CHANGE_USER_DEF_VALUES);
            }

            UserDefaultValuesModel? data = JsonConvert.DeserializeObject<UserDefaultValuesModel>(serializedValues);
            if (data == null)
            {
                return new ServiceActionResult<UserDefaultValuesModel>(HttpStatusCode.BadRequest, string.Format(Constants.MSG_WORKOUT_FAILED_TO_DESERIALIZE_OBJ, "UserDefaultValuesModel"));
            }

            string validationErrors = Utils.ValidateModel(data);
            if (!string.IsNullOrEmpty(validationErrors))
            {
                return new ServiceActionResult<UserDefaultValuesModel>(HttpStatusCode.BadRequest, validationErrors);
            }

            var oldWeightUnit = 0L;
            var existing = await GetUserDefaultValues(data.MGExerciseId, userId);
            if (existing == null)
            {
                return new ServiceActionResult<UserDefaultValuesModel>(HttpStatusCode.NotFound, Constants.MSG_FAILED_TO_UPDATE_DEFAULT_VALUES);
            }

            if (existing.MGExeciseId == 0 && data.MGExerciseId > 0)
            {
                // If the existing returned with MGExeciseId = 0 and data.MGExerciseId > 0
                // this means we are trying to create default values for specific exercise
                // (GetUserDefaultValues return 0, because the record does not exist yet
                // and the default user values was returned)
                var addResult = await AddExerciseDefaultValues(data, userId);

                if (!addResult.IsSuccess())
                {
                    return new ServiceActionResult<UserDefaultValuesModel>((HttpStatusCode) addResult.Code, Constants.MSG_FAILED_TO_UPDATE_DEFAULT_VALUES);

                }

                return new ServiceActionResult<UserDefaultValuesModel>(HttpStatusCode.Created, Constants.MSG_DEF_VALUES_UPDATED);
            }

            // Find the unit record and set the code, the model contains the Text column
            var unitRecord = await DBAccess.WeightUnits.Where(w => w.Id == data.WeightUnit.Id).FirstOrDefaultAsync();
            var unitId = 0L;

            if (unitRecord == null)
            {
                unitId = existing.WeightUnitId;
            }
            else
            {
                unitId = unitRecord.Id;
            }

            // Store the old weight unit
            oldWeightUnit = existing.WeightUnitId;

            // Change the record
            existing.Sets = data.Sets;
            existing.Reps = data.Reps;
            existing.Weight = data.Weight;
            existing.Rest = data.Rest;
            existing.Completed = data.Completed;
            existing.WeightUnitId = unitId;

            DBAccess.Entry(existing).State = EntityState.Modified;

            // If the weight unit has changed, change all records for the user to use the new weight unit
            if (oldWeightUnit != unitId)
            {
                var records = await DBAccess.UserDefaultValues.Where(u => u.UserId == userId && u.MGExeciseId > 0).ToListAsync();

                if (records != null && records.Count > 0)
                {
                    foreach (UserDefaultValue r in records)
                    {
                        r.WeightUnitId = unitId;
                        DBAccess.Entry(r).State = EntityState.Modified;
                    }
                }
            }

            await DBAccess.SaveChangesAsync();

            return new ServiceActionResult<UserDefaultValuesModel>(HttpStatusCode.OK, Constants.MSG_DEF_VALUES_UPDATED,
                                            [await ModelMapper.MapToUserDefaultValuesModel(existing, DBAccess)]);

        }

        public async Task<ServiceActionResult<BaseModel>> CreateUserProfile(string userId, string email)
        {

            var profile = new Data.Models.UserProfile
            {
                FullName = email.Substring(0, email.IndexOf("@")),
                ProfileImage = [],
                UserId = userId
            };

            await DBAccess.UserProfiles.AddAsync(profile);
            await DBAccess.SaveChangesAsync();

            return new ServiceActionResult<BaseModel>(HttpStatusCode.Created);
        }

        public async Task<ServiceActionResult<UserModel>> UpdateUserProfile(Dictionary<string, string> requestData)
        {
            /// Check if new pass is provided
            if (!requestData.TryGetValue("user", out string? serializedUser))
            {
                return new ServiceActionResult<UserModel>(HttpStatusCode.BadRequest, Constants.MSG_CHANGE_USER_DEF_VALUES);
            }

            UserModel? data = JsonConvert.DeserializeObject<UserModel>(serializedUser);
            if (data == null)
            {
                return new ServiceActionResult<UserModel>(HttpStatusCode.BadRequest, string.Format(Constants.MSG_WORKOUT_FAILED_TO_DESERIALIZE_OBJ, "UserModel"));
            }

            var validationErrors = Utils.ValidateModel(data);
            if (!string.IsNullOrEmpty(validationErrors))
            {
                return new ServiceActionResult<UserModel>(HttpStatusCode.BadRequest, validationErrors);
            }

            var profile = await DBAccess.UserProfiles.Where(p => p.UserId == data.Id).FirstOrDefaultAsync();
            if (profile == null)
            {
                return new ServiceActionResult<UserModel>(HttpStatusCode.NotFound, Constants.MSG_FAILED_TO_UPDATE_USER_PROFILE);
            }

            profile.FullName = data.FullName;
            profile.ProfileImage = Utils.DecodeBase64ToByteArray(data.ProfileImage);

            DBAccess.Entry(profile).State = EntityState.Modified;
            await DBAccess.SaveChangesAsync();

            return new ServiceActionResult<UserModel>(HttpStatusCode.OK, Constants.MSG_USER_PROFILE_UPDATED, [data]);
        }

        public async Task<ServiceActionResult<UserDefaultValuesModel>> GetExerciseOrUserDefaultValues(long mgExerciseId, string userId)
        {
            // Search for the exercise specific values, if they don't exist,
            // user default values will be returned
            var values = await GetUserDefaultValues(mgExerciseId, userId);

            if (values == null)
            {
                return new ServiceActionResult<UserDefaultValuesModel>(HttpStatusCode.NotFound, Constants.MSG_FAILED_TO_FETCH_DEFAULT_VALUES);
            }

            // Return the data, making sure tghe values we are returning are returned with the correct mgExerciseId
            // Even if we return the default values which has mgExerciseId = 0, on the client side, we need to know
            // that we are editing exercise specific values, altough they may be initially fetched with mgExerciseId 0 
            // because there are no exercise specific values yet
            values.MGExeciseId = mgExerciseId;

            return new ServiceActionResult<UserDefaultValuesModel>(HttpStatusCode.OK, Constants.MSG_SUCCESS, 
                                            [await ModelMapper.MapToUserDefaultValuesModel(values, DBAccess)]);
        }

        /// <summary>
        ///     Create record in ExerciseDefaultValue with the default values for the exercise
        /// </summary>
        /// <param name="userId">
        ///     The user id
        /// </param>
        private async Task<ServiceActionResult<BaseModel>> AddExerciseDefaultValues(UserDefaultValuesModel data, string userId)
        {
            var values = new UserDefaultValue
            {
                Sets = data.Sets,
                Reps = data.Reps,
                Weight = data.Weight,
                Rest = data.Rest,
                WeightUnitId = data.WeightUnit.Id,
                Completed = data.Completed,
                UserId = userId,
                MGExeciseId = data.MGExerciseId
            };

            await DBAccess.UserDefaultValues.AddAsync(values);
            await DBAccess.SaveChangesAsync();

            return new ServiceActionResult<BaseModel>(HttpStatusCode.OK);
        }
    }
}
