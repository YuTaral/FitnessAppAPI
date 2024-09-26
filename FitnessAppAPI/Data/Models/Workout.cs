﻿using FitnessAppAPI.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FitnessAppAPI.Data.Models
{
    /// <summary>
    ///     Workout class to represent a row of database table workouts.
    /// </summary>
    public class Workout
    {
        [Required]
        [Key]
        public long Id { get; set; }

        [Required]
        [MaxLength(Constants.DBConstants.WorkoutNameMaxLen)]
        public required string Name { get; set; }

        [Required]
        [ForeignKey("AspNetUsers")]
        public required string UserId { get; set; }

        [Required]
        public DateTime Date { get; set; }
    }
}
