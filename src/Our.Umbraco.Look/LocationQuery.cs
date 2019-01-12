﻿using Our.Umbraco.Look.Extensions;

namespace Our.Umbraco.Look
{
    public class LocationQuery
    {
        /// <summary>
        /// The location to calculate distance from - when null = feature disabled
        /// </summary>
        public Location Location { get; set; } = null;

        /// <summary>
        ///
        /// </summary>
        public Distance MaxDistance { get; set; } = null;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="location"></param>
        /// <param name="maxDistance"></param>
        public LocationQuery(Location location = null, Distance maxDistance = null)
        {
            this.Location = location;
            this.MaxDistance = maxDistance;
        }

        public override bool Equals(object obj)
        {
            var locationQuery = obj as LocationQuery;

            return locationQuery != null
                && locationQuery.Location.BothNullOrEquals(this.Location)
                && locationQuery.MaxDistance.BothNullOrEquals(this.MaxDistance);
        }

        internal LocationQuery Clone()
        {
            var clone = new LocationQuery();

            clone.Location = this.Location?.Clone();
            clone.MaxDistance = this.MaxDistance?.Clone();

            return clone;
        }
    }
}
