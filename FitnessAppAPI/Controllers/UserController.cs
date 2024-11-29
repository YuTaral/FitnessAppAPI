﻿using FitnessAppAPI.Common;
using FitnessAppAPI.Data.Services;
using FitnessAppAPI.Data.Services.User.Models;
using FitnessAppAPI.Data.Services.Workouts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NuGet.Protocol;

namespace FitnessAppAPI.Controllers
{
    /// <summary>
    ///     User Controller
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class UserController(IUserService s, IWorkoutService workoutS) : BaseController
    {
        /// <summary>
        //      UserService instance
        /// </summary>
        private readonly IUserService service = s;

        /// <summary>
        //      WorkoutService instance
        /// </summary>
        private readonly IWorkoutService workoutService = workoutS;

        /// <summary>
        //      POST request to login the user
        /// </summary>
        [HttpPost("login")]
        public ActionResult Login([FromBody] Dictionary<string, string> requestData)
        {
            /// Check if username and password are provided
            if (!requestData.TryGetValue("email", out string? email) || !requestData.TryGetValue("password", out string? password))
            {
                return CustomResponse(Constants.ResponseCode.FAIL, Constants.MSG_REG_FAIL);
            }

            LoginResponseModel? model = service.Login(email, password);

            // Success check
            if (model == null)
            {
                return CustomResponse(Constants.ResponseCode.UNEXPECTED_ERROR, Constants.MSG_LOGIN_FAILED);
            }

            // Construct the return data list
            var returnData = new List<string> { model.User.ToJson(), model.Token };

            // Get the last workout
            var result = workoutService.GetLastWorkout(model.User.Id);

            if (result.IsSuccess() && result.ResponseData.Count > 0) 
            {
                // If workout exists for the user, add it to the returnData
                returnData.Add(result.ResponseData[0].ToJson());
            }

            return CustomResponse(result.ResponseCode, result.ResponseMessage, returnData);
        }

        /// <summary>
        //      POST request to register the user
        /// </summary>
        [HttpPost("register")]
        public ActionResult Register([FromBody] Dictionary<string, string> requestData)
        {
            /// Check if username and password are provided
            if (!requestData.TryGetValue("email", out string? email) || !requestData.TryGetValue("password", out string? password))
            {
                return CustomResponse(Constants.ResponseCode.FAIL, Constants.MSG_REG_FAIL);
            }

            return CustomResponse(service.Register(email, password));
        }

        /// <summary>
        //      POST request to logout the user
        /// </summary>
        [HttpPost("logout")]
        [Authorize]
        public ActionResult Logout()
        {
            service.Logout(GetUserId());

            // Double check the user is logged out successfully
            var loggedOut = GetUserId() != "";

            if (loggedOut) { 
                return CustomResponse(Constants.ResponseCode.SUCCESS, Constants.MSG_SUCCESS);
            }

            return CustomResponse(Constants.ResponseCode.UNEXPECTED_ERROR, Constants.MSG_UNEXPECTED_ERROR);
        }

        /// <summary>
        //      POST request to change password
        /// </summary>
        [HttpPost("change-password")]
        public ActionResult ChangePassword([FromBody] Dictionary<string, string> requestData)
        {
            /// Check if new pass is provided
            if (!requestData.TryGetValue("oldPassword", out string? oldPassword) || !requestData.TryGetValue("password", out string? password))
            {
                return CustomResponse(Constants.ResponseCode.FAIL, Constants.MSG_CHANGE_PASS_FAIL);
            }

            return CustomResponse(service.ChangePassword(oldPassword, password, GetUserId()));
        }
    }
}
