using Microsoft.EntityFrameworkCore;
using RMuseum.DbContext;
using RMuseum.Models.Divan;
using RSecurityBackend.Models.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RMuseum.Services.Implementation
{
    /// <summary>
    /// geo location service implementation
    /// </summary>
    public class GeoLocationService : IGeoLocationService
    {
        /// <summary>
        /// add new location
        /// </summary>
        /// <param name="name"></param>
        /// <param name="latitude"></param>
        /// <param name="longitude"></param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanGeoLocation>> AddLocationAsync(string name, double latitude, double longitude)
        {
            try
            {
                name = name == null ? "" : name.Trim();
                if (string.IsNullOrEmpty(name))
                    return new RServiceResult<DivanGeoLocation>(null, "نام اجباری است.");
                if (null != await _context.DivanGeoLocations.Where(l => l.Name == name).FirstOrDefaultAsync())
                    return new RServiceResult<DivanGeoLocation>(null, "نام  تکراری است.");
                if (null != await _context.DivanGeoLocations.Where(l => l.Latitude == latitude && l.Longitude == longitude).FirstOrDefaultAsync())
                    return new RServiceResult<DivanGeoLocation>(null, "مختصات  تکراری است.");

                DivanGeoLocation location = new DivanGeoLocation()
                {
                    Name = name,
                    Latitude = latitude,
                    Longitude = longitude
                };
                _context.DivanGeoLocations.Add(location);
                await _context.SaveChangesAsync();
                return new RServiceResult<DivanGeoLocation>(location);                   
            }
            catch (Exception exp)
            {
                return new RServiceResult<DivanGeoLocation>(null, exp.ToString());
            }
        }

        /// <summary>
        /// update an existing location
        /// </summary>
        /// <param name="updated"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> UpdateLocationAsync(DivanGeoLocation updated)
        {
            try
            {
                var location = await _context.DivanGeoLocations.Where(l => l.Id == updated.Id).SingleOrDefaultAsync();
                if (location == null)
                    return new RServiceResult<bool>(false, "اطلاعات مکان یافت نشد.");
                updated.Name = updated.Name.Trim();
                if (string.IsNullOrEmpty(updated.Name))
                    return new RServiceResult<bool>(false, "نام اجباری است.");
                if (null != await _context.DivanGeoLocations.Where(l => l.Name == updated.Name && l.Id != updated.Id).FirstOrDefaultAsync())
                    return new RServiceResult<bool>(false, "نام  تکراری است.");
                if (null != await _context.DivanGeoLocations.Where(l => l.Latitude == updated.Latitude && l.Longitude == updated.Longitude && l.Id != updated.Id).FirstOrDefaultAsync())
                    return new RServiceResult<bool>(false, "مختصات  تکراری است.");

                location.Name = updated.Name;
                location.Latitude = updated.Latitude;
                location.Longitude = updated.Longitude;
                _context.Update(location);

                await _context.SaveChangesAsync();
                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }

        /// <summary>
        /// delete location
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> DeleteLocationAsync(int id)
        {
            try
            {
                var location = await _context.DivanGeoLocations.Where(l => l.Id == id).SingleOrDefaultAsync();
                if (location == null)
                    return new RServiceResult<bool>(false, "اطلاعات طرح مکان یافت نشد.");

                _context.Remove(location);

                await _context.SaveChangesAsync();
                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }

        /// <summary>
        /// get location by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanGeoLocation>> GetLocationAsync(int id)
        {
            try
            {
                return new RServiceResult<DivanGeoLocation>
                    (
                    await _context.DivanGeoLocations
                    .Where(l => l.Id == id)
                    .SingleOrDefaultAsync()
                    );
            }
            catch (Exception exp)
            {
                return new RServiceResult<DivanGeoLocation>(null, exp.ToString());
            }
        }

        /// <summary>
        /// get all locations
        /// </summary>
        /// <returns></returns>
        public async Task<RServiceResult<DivanGeoLocation[]>> GetLocationsAsync()
        {
            try
            {
                return new RServiceResult<DivanGeoLocation[]>
                    (
                    await _context.DivanGeoLocations
                    .OrderBy(l => l.Name).ToArrayAsync()
                    );
            }
            catch (Exception exp)
            {
                return new RServiceResult<DivanGeoLocation[]>(null, exp.ToString());
            }
        }


        /// <summary>
        /// Database Context
        /// </summary>
        protected readonly RMuseumDbContext _context;

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="context"></param>
        public GeoLocationService(RMuseumDbContext context)
        {
            _context = context;
        }
    }
}
