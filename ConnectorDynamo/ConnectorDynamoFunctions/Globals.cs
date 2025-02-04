﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.DesignScript.Runtime;

namespace Speckle.ConnectorDynamo.Functions
{
  [IsVisibleInDynamoLibrary(false)]
  public static class Globals
  {
    /// <summary>
    /// Cached Revit Document, required to properly scale incoming / outcoming geometry
    /// </summary>
    public static object RevitDocument { get; set; }
  }
}
