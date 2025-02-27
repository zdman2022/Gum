﻿using System;
using Gum.DataTypes;
using System.ComponentModel;
using Gum.Settings;

namespace Gum.PropertyGridHelpers
{
    public class GumProjectSavePropertyGridDisplayer : PropertyGridDisplayer
    {
        ReflectingPropertyDescriptorHelper mHelper = new ReflectingPropertyDescriptorHelper();

        public GumProjectSave GumProjectSave
        {
            get;
            set;
        }

        public GeneralSettingsFile GeneralSettings
        {
            get;
            set;
        }

        public override System.ComponentModel.PropertyDescriptorCollection GetProperties(Attribute[] attributes)
        {
            PropertyDescriptorCollection pdc = mHelper.GetEmpty();

            mHelper.CurrentInstance = GumProjectSave;

            mHelper.Include(GumProjectSave, "DefaultCanvasHeight", ref pdc);
            mHelper.Include(GumProjectSave, "DefaultCanvasWidth", ref pdc);
            mHelper.Include(GumProjectSave, "ShowOutlines", ref pdc);
            mHelper.Include(GumProjectSave, "RestrictToUnitValues", ref pdc);


            return pdc;
        }
    }
}
