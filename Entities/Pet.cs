﻿using System;

namespace YiX.Entities
{
    [Serializable]
    public class Pet : YiObj
    {
        public YiObj Owner { get; set; }
        public override string ToString() => "[Pet] "+UniqueId;
    }
}