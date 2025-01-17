﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using UELib.Types;

namespace UELib.Core
{
    /// <summary>
    /// [Default]Properties values deserializer.
    /// </summary>
    public sealed class UDefaultProperty : IUnrealDecompilable
    {
        [Flags]
        public enum DeserializeFlags : byte
        {
            None                    = 0x00,
            WithinStruct            = 0x01,
            WithinArray             = 0x02,
            Complex                 = WithinStruct | WithinArray,
        }

        private const byte          DoNotAppendName     = 0x01;
        private const byte          ReplaceNameMarker   = 0x02;

        private const int           V3                  = 220;
        private const int           VEnumName           = 633;
        private const int           VBoolSizeToOne      = 673;

        private IUnrealStream       _Buffer{ get{ return _Container.Buffer; } }
        private readonly UObject    _Container;
        private UStruct             _Outer;

        internal long               _BeginOffset{ get; set; }
        private long                _ValueOffset{ get; set; }
        private long                _EndOffset{ get{ return _ValueOffset + Size; } }
        private byte                _TempFlags{ get; set; }

        #region Serialized Members
        /// <summary>
        /// Name of the UProperty.
        /// </summary>
        public UName                Name{ get; private set; }

        /// <summary>
        /// Name of the UStruct. If type equals StructProperty.
        /// </summary>
        private UName               ItemName{ get; set; }

        /// <summary>
        /// Name of the UEnum. If Type equals ByteProperty.
        /// </summary>
        private UName               EnumName{ get; set; }

        /// <summary>
        /// See PropertysType enum in UnrealFlags.cs
        /// </summary>
        private PropertyType        Type{ get; set; }

        /// <summary>
        /// The stream size of this DefaultProperty.
        /// </summary>
        private int                 Size{ get; set; }

        /// <summary>
        /// Whether this property is part of an static array, and the index into it
        /// </summary>
        public int                  ArrayIndex = -1;

        /// <summary>
        /// Value of the UBoolProperty. If Type equals BoolProperty.
        /// </summary>
        private bool                _BoolValue;
        #endregion

        #region Constructors
        public UDefaultProperty( UObject owner, UStruct outer = null )
        {
            _Container = owner;
            _Outer = (outer ?? _Container as UStruct) ?? _Container.Outer as UStruct;
        }

        private int DeserializeSize( byte sizePack )
        {
            switch( sizePack )
            {
                case 0x00:
                    return 1;

                case 0x10:
                    return 2;

                case 0x20:
                    return 4;

                case 0x30:
                    return 12;

                case 0x40:
                    return 16;

                case 0x50:
                    return _Buffer.ReadByte();

                case 0x60:
                    return _Buffer.ReadInt16();

                case 0x70:
                    return _Buffer.ReadInt32();

                default:
                    throw new NotImplementedException( String.Format( "Unknown sizePack {0}", sizePack ) );
            }
        }

        const byte ArrayIndexMask = 0x80;

        private int DeserializeArrayIndex()
        {
            int arrayIndex;
#if DEBUG || BINARYMETADATA
            var startPos = _Buffer.Position;
#endif
            byte b = _Buffer.ReadByte();
            if( (b & ArrayIndexMask) == 0 )
            {
                arrayIndex = b;
            }
            else if( (b & 0xC0) == ArrayIndexMask )
            {
                arrayIndex = ((b & 0x7F) << 8) + _Buffer.ReadByte();
            }
            else
            {
                arrayIndex = ((b & 0x3F) << 24)
                    + (_Buffer.ReadByte() << 16)
                    + (_Buffer.ReadByte() << 8)
                    + _Buffer.ReadByte();
            }
#if DEBUG || BINARYMETADATA
            _Buffer.LastPosition = startPos;
#endif
            return arrayIndex;
        }

        public bool Deserialize()
        {
            _BeginOffset = _Buffer.Position;

            Name = _Buffer.ReadNameReference();
            _Container.Record( "Name", Name );
            if( Name.IsNone() )
            {
                return false;
            }

            // Unreal Engine 1 and 2
            if( _Buffer.Version < V3 )
            {
                const byte typeMask = 0x0F;
                const byte sizeMask = 0x70;

                // Packed byte
                var info = _Buffer.ReadByte();
                _Container.Record( String.Format(
                    "Info(Type={0},SizeMask=0x{1:X2},ArrayIndexMask=0x{2:X2})",
                    (PropertyType)(byte)(info & typeMask),
                    (byte)(info & sizeMask),
                    (info & ArrayIndexMask)
                    ), info
                );

                Type = (PropertyType)(byte)(info & typeMask);
                if( Type == PropertyType.StructProperty )
                {
                    ItemName = _Buffer.ReadNameReference();
                    _Container.Record( "ItemName", ItemName );
                }

                Size = DeserializeSize( (byte)(info & sizeMask) );
                if( Size >= 0x50 )
                {
                    _Container.Record( "Size", Size );
                }

                switch( Type )
                {
                    case PropertyType.BoolProperty:
                        _BoolValue = (info & ArrayIndexMask) != 0;
                        break;

                    default:
                        if( (info & ArrayIndexMask) != 0 )
                        {
                            ArrayIndex = DeserializeArrayIndex();
                            _Container.Record( "ArrayIndex", ArrayIndex );
                        }
                        break;
                }
            }
            // Unreal Engine 3
            else
            {
                string typeName = _Buffer.ReadName();
                _Container.Record( "typeName", typeName );
                Type = (PropertyType)Enum.Parse( typeof(PropertyType), typeName );

                Size = _Buffer.ReadInt32();
                _Container.Record( "Size", Size );
                ArrayIndex = _Buffer.ReadInt32();
                _Container.Record( "ArrayIndex", ArrayIndex );

                switch( Type )
                {
                    case PropertyType.StructProperty:
                        ItemName = _Buffer.ReadNameReference();
                        _Container.Record( "ItemName", ItemName );
                        break;

                    case PropertyType.ByteProperty:
                        if( _Buffer.Version >= VEnumName )
                        {
                            EnumName = _Buffer.ReadNameReference();
                            _Container.Record( "EnumName", EnumName );
                        }
                        break;

                    case PropertyType.BoolProperty:
                        _BoolValue = _Buffer.Version >= VBoolSizeToOne ? _Buffer.ReadByte() > 0 : _Buffer.ReadInt32() > 0;
                        _Container.Record( "_BoolValue", _BoolValue );
                        break;
                }
            }

            _ValueOffset = _Buffer.Position;
            try
            {
                DeserializeValue();
            }
            finally
            {
                // Even if something goes wrong, we can still skip everything and safely deserialize the next property if any!
                _Buffer.Position = _EndOffset;
            }
            return true;
        }

        /// <summary>
        /// Deserialize the value of this UPropertyTag instance.
        ///
        /// Note:
        ///     Only call after the whole package has been deserialized!
        /// </summary>
        /// <returns>The deserialized value if any.</returns>
        private string DeserializeValue( DeserializeFlags deserializeFlags = DeserializeFlags.None )
        {
            if( _Buffer == null )
            {
                return "_Buffer is not initialized!";
            }

            _Buffer.Seek( _ValueOffset, System.IO.SeekOrigin.Begin );
            try
            {
                return DeserializeDefaultPropertyValue( Type, ref deserializeFlags );
            }
            catch( DeserializationException e )
            {
                return e.ToString();
            }
        }

        /// <summary>
        /// Deserialize a default property value of a specified type.
        /// </summary>
        /// <param name="type">Kind of type to try deserialize.</param>
        /// <returns>The deserialized value if any.</returns>
        private string DeserializeDefaultPropertyValue( PropertyType type, ref DeserializeFlags deserializeFlags )
        {
            if( _Buffer.Position - _ValueOffset > Size )
            {
                throw new DeserializationException( "End of defaultproperties stream reached..." );
            }

            var orgOuter = _Outer;
            string propertyValue = String.Empty;
            try
            {
                // Deserialize Value
                switch( type )
                {
                    case PropertyType.BoolProperty:
                        {
                            var value = _BoolValue;
                            if( Size == 1 && _Buffer.Version < V3 )
                            {
                                value = _Buffer.ReadByte() > 0;
                            }
                            propertyValue = value ? "true" : "false";
                            break;
                        }

                    case PropertyType.StrProperty:
                        propertyValue = "\"" + _Buffer.ReadText().Escape() + "\"";
                        break;

                    case PropertyType.NameProperty:
                        propertyValue = "(name)\"" + _Buffer.ReadName() + "\"";
                        break;

                    case PropertyType.IntProperty:
                        propertyValue = _Buffer.ReadInt32().ToString( CultureInfo.InvariantCulture );
                        break;

#if BIOSHOCK
                    case PropertyType.QwordProperty:
                        propertyValue = _Buffer.ReadInt64().ToString( CultureInfo.InvariantCulture );
                        break;

                    case PropertyType.XWeakReferenceProperty:
                        propertyValue = "/* XWeakReference: (?=" + _Buffer.ReadName() + ",?=" + _Buffer.ReadName() + ",?=" + _Buffer.ReadByte() + ",?=" + _Buffer.ReadName() + ") */";
                        break;
#endif

                    case PropertyType.FloatProperty:
                        propertyValue = _Buffer.ReadFloat().ToUFloat();
                        break;

                    case PropertyType.ByteProperty:
                        if( _Buffer.Version >= V3 && Size == 8 )
                        {
                            var enumValue = _Buffer.ReadName();
                            propertyValue = enumValue;
                            if (propertyValue == "None")
                            {
                                propertyValue = "default";
                            }
                            else if( _Buffer.Version >= VEnumName )
                            {
                                propertyValue = EnumName + "." + propertyValue;
                            }
                            else if (float.TryParse(propertyValue, out _) == false)
                            {
                                var property = FindProperty( out _Outer ) as UByteProperty;
                                for (UStruct p = property?.Outer as UStruct; p != null; p = p.Super as UStruct)
                                {
                                    if (p.Variables?.FirstOrDefault(x => x.Name == Name) is UProperty o)
                                    {
                                        propertyValue = o.GetFriendlyPropType() + "." + propertyValue;
                                    }
                                }
                            }
                        }
                        else
                        {
                            propertyValue = _Buffer.ReadByte().ToString( CultureInfo.InvariantCulture );
                        }
                        break;

                    case PropertyType.InterfaceProperty:
                    case PropertyType.ComponentProperty:
                    case PropertyType.ObjectProperty:
                        {
                            var obj = _Buffer.ReadObject();
                            _Container.Record( "object", obj );
                            if( obj == null )
                            {
                                // =none
                                propertyValue = "default";
                                break;
                            }

                            if( obj.GetClassName() == "Class" )
                            {
                                propertyValue = $"ClassT<{obj.GetOuterGroup()}>()";
                            }
                            // If true, object is an archetype or subobject.
                            else if( obj.Outer is UPackage == false && (deserializeFlags & DeserializeFlags.WithinStruct) == 0 )
                            {
                                // Object references
                                // Unknown objects are only deserialized on demand.
                                obj.BeginDeserializing();
                                propertyValue = obj.Decompile();
                            }
                            else
                            {
                                // Assets
                                // =CLASS'Package.Group(s)+.Name'
                                propertyValue = $"LoadAsset<{obj.GetClassName()}>(\"{obj.GetOuterGroup()}\")";
                            }
                            propertyValue += String.Format( "/*Ref {0}\'{1}\'*/", obj.GetClassName(), obj.GetOuterGroup() );
                            break;
                        }

                    case PropertyType.ClassProperty:
                        {
                            var obj = _Buffer.ReadObject();
                            _Container.Record( "object", obj );
                            propertyValue = (obj != null ? $"ClassT<{obj.Name}>()" : "default");
                            break;
                        }

                    case PropertyType.DelegateProperty:
                        {
                            _TempFlags |= DoNotAppendName;
                            int outerIndex = _Buffer.ReadObjectIndex(); // Where the assigned delegate property exists.
                            var delegateValue = _Buffer.ReadName();
                            string delegateName = Name;//((string)(Name)).Substring( 2, Name.Length - 12 );

                            var del = ( 
                                from p in UnrealConfig.SharedPackages
                                from e in p.Exports
                                where e.Object is UDelegateProperty && e.Object.Name == Name
                                select ( e.Object as UDelegateProperty ).FunctionObject as UFunction ).First();

                            var enumParamNames = string.Join(", ", (from p in del.Params where p != del.ReturnProperty select p.Name)) ;
                            
                            propertyValue = $"({enumParamNames}) => {delegateValue}({enumParamNames})";
                            break;
                        }

                    #region HardCoded Struct Types
                    case PropertyType.Color:
                        {
                            string b = DeserializeDefaultPropertyValue( PropertyType.ByteProperty, ref deserializeFlags );
                            string g = DeserializeDefaultPropertyValue( PropertyType.ByteProperty, ref deserializeFlags );
                            string r = DeserializeDefaultPropertyValue( PropertyType.ByteProperty, ref deserializeFlags );
                            string a = DeserializeDefaultPropertyValue( PropertyType.ByteProperty, ref deserializeFlags );

                            propertyValue += "R=" + r +
                                ",G=" + g +
                                ",B=" + b +
                                ",A=" + a;
                            break;
                        }

                    case PropertyType.LinearColor:
                        {
                            string b = DeserializeDefaultPropertyValue( PropertyType.FloatProperty, ref deserializeFlags );
                            string g = DeserializeDefaultPropertyValue( PropertyType.FloatProperty, ref deserializeFlags );
                            string r = DeserializeDefaultPropertyValue( PropertyType.FloatProperty, ref deserializeFlags );
                            string a = DeserializeDefaultPropertyValue( PropertyType.FloatProperty, ref deserializeFlags );

                            propertyValue += "R=" + r +
                                ",G=" + g +
                                ",B=" + b +
                                ",A=" + a;
                            break;
                        }

                    case PropertyType.Vector:
                        {
                            string x = DeserializeDefaultPropertyValue( PropertyType.FloatProperty, ref deserializeFlags );
                            string y = DeserializeDefaultPropertyValue( PropertyType.FloatProperty, ref deserializeFlags );
                            string z = DeserializeDefaultPropertyValue( PropertyType.FloatProperty, ref deserializeFlags );

                            propertyValue += "X=" + x +
                                ",Y=" + y +
                                ",Z=" + z;
                            break;
                        }

                    case PropertyType.TwoVectors:
                        {
                            string v1 = DeserializeDefaultPropertyValue( PropertyType.Vector, ref deserializeFlags );
                            string v2 = DeserializeDefaultPropertyValue( PropertyType.Vector, ref deserializeFlags );

                            propertyValue += "v1={" + v1 + "},v2={" + v2 + "}";
                            break;
                        }

                    case PropertyType.Vector4:
                        {
                            string plane = DeserializeDefaultPropertyValue( PropertyType.Plane, ref deserializeFlags );

                            propertyValue += plane;
                            break;
                        }

                    case PropertyType.Vector2D:
                        {
                            string x = DeserializeDefaultPropertyValue( PropertyType.FloatProperty, ref deserializeFlags );
                            string y = DeserializeDefaultPropertyValue( PropertyType.FloatProperty, ref deserializeFlags );

                            propertyValue += "X=" + x +
                                ",Y=" + y;
                            break;
                        }

                    case PropertyType.Rotator:
                        {
                            string pitch = DeserializeDefaultPropertyValue( PropertyType.IntProperty, ref deserializeFlags );
                            string yaw = DeserializeDefaultPropertyValue( PropertyType.IntProperty, ref deserializeFlags );
                            string roll = DeserializeDefaultPropertyValue( PropertyType.IntProperty, ref deserializeFlags );

                            propertyValue += "Pitch=" + pitch +
                                ",Yaw=" + yaw +
                                ",Roll=" + roll;
                            break;
                        }

                    case PropertyType.Guid:
                        {
                            string a = DeserializeDefaultPropertyValue( PropertyType.IntProperty, ref deserializeFlags );
                            string b = DeserializeDefaultPropertyValue( PropertyType.IntProperty, ref deserializeFlags );
                            string c = DeserializeDefaultPropertyValue( PropertyType.IntProperty, ref deserializeFlags );
                            string d = DeserializeDefaultPropertyValue( PropertyType.IntProperty, ref deserializeFlags );

                            propertyValue += "A=" + a +
                                ",B=" + b +
                                ",C=" + c +
                                ",D=" + d;
                            break;
                        }

                    case PropertyType.Sphere:
                    case PropertyType.Plane:
                        {
                            string v = DeserializeDefaultPropertyValue( PropertyType.Vector, ref deserializeFlags );
                            string w = DeserializeDefaultPropertyValue( PropertyType.FloatProperty, ref deserializeFlags );

                            propertyValue += v + ",W=" + w;
                            break;
                        }

                    case PropertyType.Scale:
                        {
                            string scale = DeserializeDefaultPropertyValue( PropertyType.Vector, ref deserializeFlags );
                            string sheerRate = DeserializeDefaultPropertyValue( PropertyType.FloatProperty, ref deserializeFlags );
                            string sheerAxis = DeserializeDefaultPropertyValue( PropertyType.ByteProperty, ref deserializeFlags );

                            propertyValue += "Scale={" + scale + "}" +
                                ",SheerRate=" + sheerRate +
                                ",SheerAxis=" + sheerAxis;
                            break;
                        }

                    case PropertyType.Box:
                        {
                            string min = DeserializeDefaultPropertyValue( PropertyType.Vector, ref deserializeFlags );
                            string max = DeserializeDefaultPropertyValue( PropertyType.Vector, ref deserializeFlags );
                            string isValid = DeserializeDefaultPropertyValue( PropertyType.ByteProperty, ref deserializeFlags );

                            propertyValue += "Min={" + min + "}" +
                                ",Max={" + max + "}" +
                                ",IsValid=" + isValid;
                            break;
                        }

                    /*case PropertyType.InterpCurve:
                    {
                        // HACK:
                        UPropertyTag tag = new UPropertyTag( _Owner );
                        tag.Serialize();
                        buffer.Seek( tag.ValueOffset, System.IO.SeekOrigin.Begin );

                        int curvescount = buffer.ReadIndex();
                        if( curvescount <= 0 )
                        {
                            break;
                        }
                        propertyvalue += tag.Name + "=(";
                        for( int i = 0; i < curvescount; ++ i )
                        {
                            propertyvalue += "(" + SerializeDefaultPropertyValue( PropertyType.InterpCurvePoint, buffer, ref serializeFlags ) + ")";
                            if( i != curvescount - 1 )
                            {
                                propertyvalue += ",";
                            }
                        }
                        propertyvalue += ")";
                        break;
                    }*/

                    /*case PropertyType.InterpCurvePoint:
                    {
                        string InVal = SerializeDefaultPropertyValue( PropertyType.Float, buffer, ref serializeFlags );
                        string OutVal = SerializeDefaultPropertyValue( PropertyType.Float, buffer, ref serializeFlags );

                        propertyvalue += "InVal=" + InVal +
                            ",OutVal=" + OutVal;
                        break;
                    }*/

                    case PropertyType.Quat:
                        {
                            propertyValue += DeserializeDefaultPropertyValue( PropertyType.Plane, ref deserializeFlags );
                            break;
                        }

                    case PropertyType.Matrix:
                        {
                            string xPlane = DeserializeDefaultPropertyValue( PropertyType.Plane, ref deserializeFlags );
                            string yPlane = DeserializeDefaultPropertyValue( PropertyType.Plane, ref deserializeFlags );
                            string zPlane = DeserializeDefaultPropertyValue( PropertyType.Plane, ref deserializeFlags );
                            string wPlane = DeserializeDefaultPropertyValue( PropertyType.Plane, ref deserializeFlags );
                            propertyValue += "XPlane={" + xPlane + "}" +
                                ",YPlane={" + yPlane + "}" +
                                ",ZPlane={" + zPlane + "}" +
                                ",WPlane={" + wPlane + "}";
                            break;
                        }

                    case PropertyType.IntPoint:
                        {
                            string x = DeserializeDefaultPropertyValue( PropertyType.IntProperty, ref deserializeFlags );
                            string y = DeserializeDefaultPropertyValue( PropertyType.IntProperty, ref deserializeFlags );

                            propertyValue += "X=" + x + ",Y=" + y;
                            break;
                        }
                    #endregion

                    case PropertyType.PointerProperty:
                    case PropertyType.StructProperty:
                        {
                            deserializeFlags |= DeserializeFlags.WithinStruct;
                            bool isHardCoded = false;
                            var hardcodedStructs = (PropertyType[])Enum.GetValues( typeof( PropertyType ) );
                            for( var i = (byte)PropertyType.StructOffset; i < hardcodedStructs.Length; ++ i )
                            {
                                var structType = Enum.GetName( typeof( PropertyType ), (byte)hardcodedStructs[i] );
                                if( String.Compare( ItemName, structType, StringComparison.OrdinalIgnoreCase ) != 0 )
                                    continue;

                                isHardCoded = true;
                                var v = DeserializeDefaultPropertyValue( hardcodedStructs[i], ref deserializeFlags );
                                using (UDecompilingState.TabScope())
                                    v = $"\r\n{UDecompilingState.Tabs}" + v.Replace(",", $",\r\n{UDecompilingState.Tabs}");
                                propertyValue += v;
                                break;
                            }

                            if( !isHardCoded )
                            {
                                // We have to modify the outer so that dynamic arrays within this struct
                                // will be able to find its variables to determine the array type.
                                FindProperty( out _Outer );
                                using (UDecompilingState.TabScope())
                                {
                                    var tags = new List<(UDefaultProperty tag, string deserialized)>();
                                    while( true )
                                    {
                                        var tag = new UDefaultProperty( _Container, _Outer );
                                        if( tag.Deserialize() )
                                            tags.Add( (tag, tag.DeserializeValue(deserializeFlags)) );
                                        else
                                            break;
                                    }

                                    foreach (var groupByName in from tag in tags group tag by tag.tag.Name.Text into groupByName select groupByName )
                                    {
                                        if( groupByName.ElementAtOrDefault( 1 ) == default && (groupByName.First().tag.ArrayIndex == 0/* || groupByName.First().tag.Type == PropertyType.BoolProperty*/) )
                                        {
                                            var tag = groupByName.First();
                                            propertyValue += $"\r\n{UDecompilingState.Tabs}{groupByName.Key} = {tag.deserialized},";
                                        }
                                        else
                                        {
                                            UProperty? arrType = ( from s in _Outer.EnumerateInheritance()
                                                from v in s.Variables
                                                where v.Name == groupByName.Key
                                                select v ).FirstOrDefault();
                                            
                                            string content = "";
                                            using (UDecompilingState.TabScope())
                                            {
                                                int counts = -1;
                                                bool holes = false;
                                                foreach( var tag in from i in groupByName orderby i.tag.ArrayIndex select i )
                                                {
                                                    content += $"\r\n{UDecompilingState.Tabs}[{tag.tag.ArrayIndex}] = {tag.deserialized},";
                                                    if( ++counts != tag.tag.ArrayIndex )
                                                        holes = true;
                                                }

                                                if( holes )
                                                {
                                                    content += $"\r\n{UDecompilingState.Tabs}#warning index access seems to hint that the collection is not wholly assigned to, this should probably be changed to assigning to specific indices on the existing collection instead of assigning a whole new collection";
                                                }
                                            }

                                            propertyValue += $"\r\n{UDecompilingState.Tabs}{groupByName.Key} = new {arrType?.GetFriendlyType()}()\r\n{UDecompilingState.Tabs}{{{content}\r\n{UDecompilingState.Tabs}}},";
                                        }
                                    }
                                }
                            }

                            var instanceName = (string)ItemName;
                            if (ItemName == _Outer?.Name)
                                instanceName = _Outer?.GetFriendlyType();
                            else if (string.IsNullOrEmpty(instanceName))
                                instanceName = _Outer?.GetFriendlyType();

                            propertyValue = propertyValue.Length != 0 ? $"new {instanceName}\r\n{UDecompilingState.Tabs}{{{propertyValue}\r\n{UDecompilingState.Tabs}}}" : "default";
                            break;
                        }

                    case PropertyType.ArrayProperty:
                        {
                            int arraySize = _Buffer.ReadIndex();
                            _Container.Record( "arraySize", arraySize );
                            if( arraySize == 0 )
                            {
                                propertyValue = "default";
                                break;
                            }

                            // Find the property within the outer/owner or its inheritances.
                            // If found it has to modify the outer so structs within this array can find their array variables.
                            // Additionally we need to know the property to determine the array's type.
                            var arrayType = PropertyType.None;
                            var property = FindProperty( out _Outer ) as UArrayProperty;
                            if( property != null && property.InnerProperty != null )
                            {
                                arrayType = property.InnerProperty.Type;
                            }
                            // If we did not find a reference to the associated property(because of imports)
                            // then try to determine the array's type by scanning the definined array types.
                            else if( UnrealConfig.VariableTypes != null && UnrealConfig.VariableTypes.ContainsKey( Name ) )
                            {
                                var varTuple = UnrealConfig.VariableTypes[Name];
                                if( varTuple != null )
                                {
                                    arrayType = varTuple.Item2;
                                }
                            }

                            if( arrayType == PropertyType.None )
                            {
                                propertyValue = $"/* Array type({Name}) was not detected. */";
                                break;
                            }

                            var friendlyType = "";
                            for (UStruct p = (UStruct)property.Outer; p != null; p = p.Super as UStruct)
                            {
                                if (p.Variables?.FirstOrDefault(x => x.Name == Name) is UProperty o)
                                {
                                    friendlyType = o.GetFriendlyType();
                                }
                            }
                            if(friendlyType == "")
                                Debugger.Break();
                            
                            deserializeFlags |= DeserializeFlags.WithinArray;
                            bool withinStruct = (deserializeFlags & DeserializeFlags.WithinStruct) != 0;
                            
                            // Hardcoded fix for InterpCurve and InterpCurvePoint.
                            if( withinStruct && String.Compare( Name, "Points", StringComparison.OrdinalIgnoreCase ) == 0 )
                            {
                                arrayType = PropertyType.StructProperty;
                            }

                            try
                            {
                                using (UDecompilingState.TabScope())
                                {
                                    for( int i = 0; i < arraySize; ++ i )
                                    {
                                        string elementValue = DeserializeDefaultPropertyValue( arrayType, ref deserializeFlags );
                                        if( withinStruct == false && (_TempFlags & ReplaceNameMarker) != 0 )
                                        {
                                            elementValue = elementValue.Replace( "%ARRAYNAME%=", "//"+ Name + "[" + i + "]=\r\n" + UDecompilingState.Tabs );
                                            _TempFlags = 0x00;
                                        }

                                        propertyValue += $"\r\n{UDecompilingState.Tabs}{elementValue},";
                                    }
                                }
                            }
                            finally
                            {
                                propertyValue = $"new {friendlyType}\r\n{UDecompilingState.Tabs}{{{propertyValue}\r\n{UDecompilingState.Tabs}}}";
                                _TempFlags |= DoNotAppendName;
                            }
                            break;
                        }

                    default:
                        propertyValue = "/* Unknown default property type! */";
                        break;
                }
            }
            catch( Exception e )
            {
                if( type == PropertyType.StructProperty && this.Type == PropertyType.ArrayProperty )
                    throw;
                
                return propertyValue + e.OutputWarningException( $"Exception thrown while deserializing {Name}" );
            }
            finally
            {
                _Outer = orgOuter;
            }
            return propertyValue;
        }
        #endregion

        #region Decompilation

        public string Decompile() => Decompile( false );
        public string Decompile(bool valueOnly)
        {
            _TempFlags = 0x00;
            string value;
            _Container.EnsureBuffer();
            try
            {
                value = DeserializeValue();
            }
            catch( Exception e )
            {
                value = "//" + e;
            }
            finally
            {
                _Container.MaybeDisposeBuffer();
            }

            if( valueOnly )
                return value;

            // Array or Inlined object
            if( (_TempFlags & DoNotAppendName) != 0 )
            {
                // The tag handles the name etc on its own.
                return value + ";";
            }
            string arrayindex = String.Empty;
            if( ArrayIndex > 0 && Type != PropertyType.BoolProperty )
            {
                arrayindex = $"[{ArrayIndex}]";
            }
            
            return $"{Name}{arrayindex} = {value};";
        }
        #endregion

        #region Methods
        private UProperty FindProperty( out UStruct outer )
        {
            UProperty property = null;
            outer = _Outer ?? _Container.Class as UStruct;
            for( var structField = outer; structField != null; structField = structField.Super as UStruct )
            {
                if( structField.Variables == null || !structField.Variables.Any() )
                    continue;

                property = structField.Variables.Find( i => i.Name == Name );
                if( property == null )
                    continue;

                switch( property.Type )
                {
                    case PropertyType.StructProperty:
                        outer = ((UStructProperty)property).StructObject;
                        break;

                    case PropertyType.ArrayProperty:
                        var arrayField = property as UArrayProperty;
                        Debug.Assert( arrayField != null, "arrayField != null" );
                        var arrayInnerField = arrayField.InnerProperty;
                        if( arrayInnerField.Type == PropertyType.StructProperty )
                        {
                            _Outer = ((UStructProperty)arrayInnerField).StructObject;
                        }
                        break;

                    default:
                        outer = structField;
                        break;
                }
                break;
            }
            return property;
        }
        #endregion
    }

    [System.Runtime.InteropServices.ComVisible( false )]
    public sealed class DefaultPropertiesCollection : List<UDefaultProperty>
    {
        public UDefaultProperty Find( string name )
        {
            return Find( prop => prop.Name == name );
        }

        public bool Contains( string name )
        {
            return Find( name ) != null;
        }
    }
}