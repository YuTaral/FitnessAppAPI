﻿using FitnessAppAPI.Data.Services.Workouts.Models;

namespace FitnessAppAPI.Data.Services.Workouts
{
    /// <summary>
    ///     Workout service interface define the logic for workout CRUD operations.
    /// </summary>
    public interface IWorkoutService
    {
        // Workout
        public WorkoutModel? AddWorkout(WorkoutModel data, string userId);
        public WorkoutModel? EditWorkout(WorkoutModel data, string userId);
        public bool DeleteWorkout(long workoutId);
        public WorkoutModel? GetWorkout(long id);
        public List<WorkoutModel>? GetLatestWorkouts(String userId);
        public WorkoutModel? GetLastWorkout(string userId);

        // Exercise
        public bool AddExercise(ExerciseModel set, long workoutId);
        public bool UpdateExercise(ExerciseModel exercise, long workoutId);
        public long DeleteExercise(long exerciseId);

        // Muscle Groups
        public List<MuscleGroupModel>? GetMuscleGroups(String userId);


    }
}
