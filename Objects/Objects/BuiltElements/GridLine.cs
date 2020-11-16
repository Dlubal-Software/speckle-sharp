﻿using Objects.Geometry;
using Speckle.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Objects.BuiltElements
{
  public class GridLine : Base, IGridLine
  {
    public Line baseLine { get; set; }
    public Level level { get; set; }

    public GridLine(Line baseLine, Level level = null)
    {
      this.baseLine = baseLine;
      this.level = level;
    }
  }
}
