﻿using FitnessAppAPI.Common;
using FitnessAppAPI.Data.Models;
using FitnessAppAPI.Data.Services.Exercises;
using FitnessAppAPI.Data.Services.Exercises.Models;
using FitnessAppAPI.Data.Services.UserProfile.Models;
using FitnessAppAPI.Data.Services.Workouts.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Net;

namespace FitnessAppAPI.Data.Services.Workouts
{
    /// <summary>
    ///     Workout service class to implement IWorkoutService interface.
    /// </summary>

    public class WorkoutService(FitnessAppAPIContext DB, IExerciseService eService) : BaseService(DB), IWorkoutService
    {

        /// <summary>
        //      ExerciseService instance
        /// </summary>
        private readonly IExerciseService exerciseService = eService;

        public async Task<ServiceActionResult<WorkoutModel>> AddWorkout(Dictionary<string, string> requestData, string userId)
        {
            // Check if the neccessary data is provided
            if (!requestData.TryGetValue("workout", out string? serializedWorkout))
            {
                return new ServiceActionResult<WorkoutModel>(HttpStatusCode.BadRequest, Constants.MSG_WORKOUT_ADD_FAIL_NO_DATA);
            }

            WorkoutModel? workoutData = JsonConvert.DeserializeObject<WorkoutModel>(serializedWorkout);
            if (workoutData == null)
            {
                return new ServiceActionResult<WorkoutModel>(HttpStatusCode.BadRequest, string.Format(Constants.MSG_WORKOUT_FAILED_TO_DESERIALIZE_OBJ, "WorkoutModel"));
            }

            string validationErrors = Utils.ValidateModel(workoutData);
            if (!string.IsNullOrEmpty(validationErrors))
            {
                return new ServiceActionResult<WorkoutModel>(HttpStatusCode.BadRequest, validationErrors);
            }

            var workout = new Workout
            {
                Name = workoutData.Name,
                UserId = userId,
                StartDateTime = DateTime.Now,
                FinishDateTime = null,
                Template = "N",
                DurationSeconds = 0,
                Notes = workoutData.Notes
            };

            // Add the workout to make sure id is generated
            await DBAccess.Workouts.AddAsync(workout);
            await DBAccess.SaveChangesAsync();

            // Check if this is template and add the exercises if so
            if (workoutData.Template && workoutData.Exercises != null)
            {
                foreach (ExerciseModel e in workoutData.Exercises)
                {
                    var addExerciseResult = await exerciseService.AddExerciseToWorkout(e, workout.Id);

                    if (!addExerciseResult.IsSuccess())
                    {
                        return new ServiceActionResult<WorkoutModel>((HttpStatusCode) addExerciseResult.Code, addExerciseResult.Message);
                    }
                }
            }

            return new ServiceActionResult<WorkoutModel>(HttpStatusCode.Created, Constants.MSG_WORKOUT_ADDED,
                                                [await ModelMapper.MapToWorkoutModel(workout, DBAccess)]);
        }

        public async Task<ServiceActionResult<WorkoutModel>> UpdateWorkout(Dictionary<string, string> requestData, string userId)
        {
            // Check if the neccessary data is provided
            if (!requestData.TryGetValue("workout", out string? serializedWorkout))
            {
                return new ServiceActionResult<WorkoutModel>(HttpStatusCode.BadRequest, Constants.MSG_WORKOUT_ADD_FAIL_NO_DATA);
            }

            WorkoutModel? workoutData = JsonConvert.DeserializeObject<WorkoutModel>(serializedWorkout);
            if (workoutData == null)
            {
                return new ServiceActionResult<WorkoutModel>(HttpStatusCode.BadRequest, string.Format(Constants.MSG_WORKOUT_FAILED_TO_DESERIALIZE_OBJ, "WorkoutModel"));
            }

            string validationErrors = Utils.ValidateModel(workoutData);
            if (!string.IsNullOrEmpty(validationErrors))
            {
                return new ServiceActionResult<WorkoutModel>(HttpStatusCode.BadRequest, validationErrors);
            }

            var workout = await CheckWorkoutExists(workoutData.Id, userId);
            if (workout == null)
            {
                return new ServiceActionResult<WorkoutModel>(HttpStatusCode.NotFound, Constants.MSG_WORKOUT_DOES_NOT_EXIST);
            }

            // Change the data
            workout.Name = workoutData.Name;
            workout.FinishDateTime = workoutData.FinishDateTime;
            workout.DurationSeconds = workoutData.DurationSeconds;
            workout.Notes = workoutData.Notes;

            DBAccess.Entry(workout).State = EntityState.Modified;
            await DBAccess.SaveChangesAsync();

            return new ServiceActionResult<WorkoutModel>(HttpStatusCode.OK, Constants.MSG_WORKOUT_UPDATED,
                                                [await ModelMapper.MapToWorkoutModel(workout, DBAccess)]);
        }

        public async Task<ServiceActionResult<BaseModel>> DeleteWorkout(long workoutId, string userId) {

            // Check if the neccessary data is provided
            if (workoutId <= 0)
            {
                return new ServiceActionResult<BaseModel>(HttpStatusCode.BadRequest, Constants.MSG_OBJECT_ID_NOT_PROVIDED);
            }

            var workout = await CheckWorkoutExists(workoutId, userId);
            if (workout == null)
            {
                return new ServiceActionResult<BaseModel>(HttpStatusCode.NotFound, Constants.MSG_WORKOUT_DOES_NOT_EXIST);
            }

            // Delete the workout
            DBAccess.Workouts.Remove(workout);
            await DBAccess.SaveChangesAsync();

            return new ServiceActionResult<BaseModel>(HttpStatusCode.OK, Constants.MSG_WORKOUT_DELETED);
        }

        public async Task<ServiceActionResult<WorkoutModel>> GetWorkout(long id, string userId) {
            var workout = await CheckWorkoutExists(id, userId);
            if (workout == null)
            {
                return new ServiceActionResult<WorkoutModel>(HttpStatusCode.NotFound, Constants.MSG_WORKOUT_DOES_NOT_EXIST, []);
            }

            return new ServiceActionResult<WorkoutModel>(HttpStatusCode.OK, Constants.MSG_SUCCESS,
                                                [await ModelMapper.MapToWorkoutModel(workout, DBAccess)]);
        }

        public async Task<ServiceActionResult<WorkoutModel>> GetLatestWorkouts(string startDate, string userId) {
            if (!DateTime.TryParse(startDate, out DateTime date))
            {
                return new ServiceActionResult<WorkoutModel>(HttpStatusCode.BadRequest, Constants.MSG_INVALID_DATE_FORMAT);
            }

            // Start the query
            var workouts = await DBAccess.Workouts.Where(w => w.UserId == userId && w.Template == "N" && w.StartDateTime >= date)
                                                    .OrderByDescending(w => w.StartDateTime)
                                                    .ToListAsync();

            // Create the list asynchonously
            var workoutModels = new List<WorkoutModel>();
            foreach (var workout in workouts)
            {
                var workoutModel = await ModelMapper.MapToWorkoutModel(workout, DBAccess);
                workoutModels.Add(workoutModel);
            }

            return new ServiceActionResult<WorkoutModel>(HttpStatusCode.OK, Constants.MSG_SUCCESS, workoutModels);
        }

        public async Task<ServiceActionResult<WeightUnitModel>> GetWeightUnits()
        {
            var units = await DBAccess.WeightUnits.Select(w => ModelMapper.MapToWeightUnitModel(w)).ToListAsync();

            if (units.Count == 0)
            {
                return new ServiceActionResult<WeightUnitModel>(HttpStatusCode.NotFound, Constants.MSG_FAILED_TO_FETCH_WEIGHT_UNITS);
            }

            return new ServiceActionResult<WeightUnitModel>(HttpStatusCode.OK, Constants.MSG_SUCCESS, units);
        }

        /// <summary>
        ///     Perform a check whether the workout exists, returns workout object if it exists,
        ///     null otherwise
        /// </summary>
        /// <param name="id">
        ///     The workout id
        /// </param>
        /// <param name="userId">
        ///     The userId owner of the workout
        /// </param>
        private async Task<Workout?> CheckWorkoutExists(long id, string userId)
        {
            return await DBAccess.Workouts.Where(w => w.Id == id && w.UserId == userId).FirstOrDefaultAsync();
        }
    }
}
