﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DigitalHealth.Web.EntitiesDto
{
    public class MethodOfTreatmentListDto
    {
        public List<MethodOfTreatmentDto> MethodOfTreatmentDtos { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int PageCount { get; set; }
    }
}