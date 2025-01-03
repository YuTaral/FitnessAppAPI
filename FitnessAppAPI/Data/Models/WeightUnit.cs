﻿using System.ComponentModel.DataAnnotations;

namespace FitnessAppAPI.Data.Models
{
    /// <summary>
    ///     WeightUnit class to represent a row of database table WeightUnits.
    /// </summary>
    public class WeightUnit
    {
        [Key]
        public long Id { get; set; }

        [MaxLength(10)]
        public required string Text { get; set; }
    }
}
