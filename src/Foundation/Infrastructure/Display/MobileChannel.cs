﻿using EPiServer.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Wangkanai.Detection;

namespace Foundation.Infrastructure.Display
{
    public class MobileChannel : DisplayChannel
    {
        public override string ChannelName => "Mobile";

        public override string ResolutionId => typeof(IphoneVerticalResolution).FullName;

        public override bool IsActive(HttpContext context)
        {
            var detection = context.RequestServices.GetRequiredService<IDetection>();
            return detection.Device.Type == DeviceType.Mobile;
        }
    }
}