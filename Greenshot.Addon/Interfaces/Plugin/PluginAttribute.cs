﻿/*
 * Greenshot - a free and open source screenshot tool
 * Copyright (C) 2007-2016 Thomas Braun, Jens Klingen, Robin Krom
 * 
 * For more information see: http://getgreenshot.org/
 * The Greenshot project is hosted on GitHub: https://github.com/greenshot
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 1 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.ComponentModel.Composition;

namespace Greenshot.Addon.Interfaces.Plugin
{
	[MetadataAttribute]
	[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
	public sealed class PluginAttribute : InheritedExportAttribute, IGreenshotPluginMetadata
	{
		public string Name
		{
			get;
			set;
		}

		public string CreatedBy
		{
			get;
			set;
		} = "Greenshot";

		public bool Configurable
		{
			get;
			set;
		} = false;

		public PluginAttribute() : base(typeof(IGreenshotPlugin))
		{
		}

		public PluginAttribute(string name) : base(typeof(IGreenshotPlugin))
		{
			Name = name;
		}
	}
}
