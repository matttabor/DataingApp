using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using DatingApp.API.Data;
using DatingApp.API.DTOs;
using DatingApp.API.Helpers;
using DatingApp.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DatingApp.API.Controllers
{
    [Authorize]
    [Route("api/users/{userId}/photos")]
    public class PhotosController : ControllerBase
    {
        private readonly IDatingRepository _repository;
        private readonly IMapper _mapper;
        private readonly IOptions<CloudinarySettings> _cloudinaryConfig;
        private readonly Cloudinary _cloudinary;

        public PhotosController(IDatingRepository repository, 
            IMapper mapper,
            IOptions<CloudinarySettings> cloudinaryConfig)
        {
            _repository = repository;
            _mapper = mapper;
            _cloudinaryConfig = cloudinaryConfig;

            Account account = new Account(cloudinaryConfig.Value.CloudName, cloudinaryConfig.Value.ApiKey, cloudinaryConfig.Value.ApiSecret);
           
           _cloudinary = new Cloudinary(account);
        }

        [HttpGet("{id}", Name = "GetPhoto")]
        public async Task<IActionResult> GetPhoto(int id)
        {
            var photoFromRepo = await _repository.GetPhoto(id);

            var photo = _mapper.Map<PhotoForReturnDto>(photoFromRepo);

            return Ok(photo);
        }



        [HttpPost]
        public async Task<IActionResult> AddPhotoForUser(int userid, [FromForm]PhotoForCreationDto photoForCreationDto)
        {
            if (userid != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            {
                return Unauthorized();
            }

            var userFromRepo = await _repository.GetUser(userid);

            var file = photoForCreationDto.File;

            var uploadResult = new ImageUploadResult();
            if(file.Length > 0)
            {
                using(var stream = file.OpenReadStream())
                {
                    var uploadParams = new ImageUploadParams
                    {
                        File = new FileDescription(file.Name, stream),
                        Transformation = new Transformation().Width(500).Height(500).Crop("fill").Gravity("face")
                    };

                    uploadResult = _cloudinary.Upload(uploadParams);
                }
            }

            photoForCreationDto.Url = uploadResult.Uri.ToString();
            photoForCreationDto.PublicId = uploadResult.PublicId;

            var photo = _mapper.Map<Photo>(photoForCreationDto);

            if(!userFromRepo.Photos.Any(u=> u.IsMain))
            {
                photo.IsMain = true;
            }

            userFromRepo.Photos.Add(photo);

            if(await _repository.SaveAll())
            {
                var PhotoForReturnDto = _mapper.Map<PhotoForReturnDto>(photo);
                return CreatedAtRoute("GetPhoto", new {id = photo.Id}, PhotoForReturnDto);
            }

            return BadRequest("Could not add the photo");
        }
    }
}