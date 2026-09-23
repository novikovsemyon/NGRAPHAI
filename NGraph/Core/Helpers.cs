using System.Diagnostics.Contracts;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.Attributes;
using Nice3point.Revit.Toolkit.External;


namespace NGraph.Core
{


    public class Helpers
    {
        private const double Tolerance = 1e-9;

        /// <summary>
        ///     Determines whether this BoundingBox overlaps with another BoundingBox
        /// </summary>
        /// <param name="source">The source <see cref="Autodesk.Revit.DB.BoundingBoxXYZ"/> instance</param>
        /// <param name="other">The <see cref="Autodesk.Revit.DB.BoundingBoxXYZ"/> instance to compare with the source</param>
        /// <returns><c>true</c> if the two <see cref="Autodesk.Revit.DB.BoundingBoxXYZ"/> instances have at least one common point</returns>
        [JetBrains.Annotations.Pure]
        public static bool Overlaps( BoundingBoxXYZ source, BoundingBoxXYZ other)
        {
            var sourceMin = source.Transform.IsIdentity ? source.Min : source.Transform.OfPoint(source.Min);
            var sourceMax = source.Transform.IsIdentity ? source.Max : source.Transform.OfPoint(source.Max);
            var otherMin = other.Transform.IsIdentity ? other.Min : other.Transform.OfPoint(other.Min);
            var otherMax = other.Transform.IsIdentity ? other.Max : other.Transform.OfPoint(other.Max);

            var overlapX = !(sourceMax.X < otherMin.X - Tolerance || sourceMin.X > otherMax.X + Tolerance);
            var overlapY = !(sourceMax.Y < otherMin.Y - Tolerance || sourceMin.Y > otherMax.Y + Tolerance);
            var overlapZ = !(sourceMax.Z < otherMin.Z - Tolerance || sourceMin.Z > otherMax.Z + Tolerance);

            return overlapX && overlapY && overlapZ;
        }
        
        //
// Сводка:
//     Returns the distance between Lines. The Lines are considered to be endless
//
// Возврат:
//     Distance between lines. Returns 0 if the lines intersect
        public static double Distance(Line line1, Line line2)
        {
            XYZ direction = line1.Direction;
            XYZ endPoint = line1.GetEndPoint(0);
            XYZ direction2 = line2.Direction;
            XYZ endPoint2 = line2.GetEndPoint(0);
            double num = endPoint.X - endPoint2.X;
            double num2 = endPoint.Y - endPoint2.Y;
            double num3 = endPoint.Z - endPoint2.Z;
            double num4 = direction.X * direction2.Y - direction2.X * direction.Y;
            double num5 = direction.Y * direction2.Z - direction2.Y * direction.Z;
            double num6 = direction.Z * direction2.X - direction2.Z * direction.X;
            double num7 = Math.Sqrt(Math.Pow(num5, 2.0) + Math.Pow(num6, 2.0) + Math.Pow(num4, 2.0));
            if (num7 < 1E-09)
            {
                double num8 = num2 * direction.X;
                double num9 = num2 * direction.Z;
                double num10 = num3 * direction.X;
                double num11 = num3 * direction.Y;
                double num12 = num * direction.Y;
                double num13 = num * direction.Z;
                return Math.Sqrt(Math.Pow(num9 - num11, 2.0) + Math.Pow(num10 - num13, 2.0) + Math.Pow(num12 - num8, 2.0)) / Math.Sqrt(Math.Pow(direction.X, 2.0) + Math.Pow(direction.Y, 2.0) + Math.Pow(direction.Z, 2.0));
            }

            return Math.Abs((num5 * num + num6 * num2 + num4 * num3) / num7);
        }
        //
// Сводка:
//     Determines whether the specified point is contained within this BoundingBox
//
// Параметры:
//   source:
//     The source Autodesk.Revit.DB.BoundingBoxXYZ instance
//
//   point:
//     The Autodesk.Revit.DB.XYZ point to check for containment within the source Autodesk.Revit.DB.BoundingBoxXYZ
//
//
//   strict:
//     true if the point needs to be fully on the inside of the source. A point coinciding
//     with the box border will be considered 'outside'.
//
// Возврат:
//     true if the specified point is within the bounds of the Autodesk.Revit.DB.BoundingBoxXYZ
        public static bool Contains(BoundingBoxXYZ source, XYZ point, bool strict)
        {
            if (!source.Transform.IsIdentity)
            {
                point = source.Transform.Inverse.OfPoint(point);
            }

            bool num = ((!strict) ? (point.X >= source.Min.X - 1E-09 && point.X <= source.Max.X + 1E-09) : (point.X > source.Min.X + 1E-09 && point.X < source.Max.X - 1E-09));
            bool flag = ((!strict) ? (point.Y >= source.Min.Y - 1E-09 && point.Y <= source.Max.Y + 1E-09) : (point.Y > source.Min.Y + 1E-09 && point.Y < source.Max.Y - 1E-09));
            bool flag2 = ((!strict) ? (point.Z >= source.Min.Z - 1E-09 && point.Z <= source.Max.Z + 1E-09) : (point.Z > source.Min.Z + 1E-09 && point.Z < source.Max.Z - 1E-09));
            return num && flag && flag2;
        }
        //
// Сводка:
//     Determines whether this Autodesk.Revit.DB.BoundingBoxXYZ contains another Autodesk.Revit.DB.BoundingBoxXYZ
//
//
// Параметры:
//   source:
//     The source Autodesk.Revit.DB.BoundingBoxXYZ instance
//
//   other:
//     The Autodesk.Revit.DB.BoundingBoxXYZ instance to compare with the source
//
//   strict:
//     true if the box needs to be fully on the inside of the source. Coincident boxes
//     will be considered 'outside'.
//
// Возврат:
//     true if the source Autodesk.Revit.DB.BoundingBoxXYZ contains the other Autodesk.Revit.DB.BoundingBoxXYZ
//     instance
        public static bool Contains(BoundingBoxXYZ source, BoundingBoxXYZ other, bool strict)
        {
            XYZ xYZ = (source.Transform.IsIdentity ? source.Min : source.Transform.OfPoint(source.Min));
            XYZ xYZ2 = (source.Transform.IsIdentity ? source.Max : source.Transform.OfPoint(source.Max));
            XYZ xYZ3 = (other.Transform.IsIdentity ? other.Min : other.Transform.OfPoint(other.Min));
            XYZ xYZ4 = (other.Transform.IsIdentity ? other.Max : other.Transform.OfPoint(other.Max));
            bool num = ((!strict) ? (xYZ3.X >= xYZ.X - 1E-09 && xYZ4.X <= xYZ2.X + 1E-09) : (xYZ3.X > xYZ.X + 1E-09 && xYZ4.X < xYZ2.X - 1E-09));
            bool flag = ((!strict) ? (xYZ3.Y >= xYZ.Y - 1E-09 && xYZ4.Y <= xYZ2.Y + 1E-09) : (xYZ3.Y > xYZ.Y + 1E-09 && xYZ4.Y < xYZ2.Y - 1E-09));
            bool flag2 = ((!strict) ? (xYZ3.Z >= xYZ.Z - 1E-09 && xYZ4.Z <= xYZ2.Z + 1E-09) : (xYZ3.Z > xYZ.Z + 1E-09 && xYZ4.Z < xYZ2.Z - 1E-09));
            return num && flag && flag2;
        }
        /// <summary>
        /// Округление 
        /// </summary>
        /// <param name="number">Округляемое число</param>
        /// <param name="round"></param>
        /// <returns></returns>
        static public int LenghtInt(int number, int round)
        {
            int ostatok = number % round;//45
            if (ostatok == 0)
            {
                return number;
            }

            else if ((double)ostatok / (double)round >= 0.5) //если остаток более 50% от округления то прибавляем 
            {
                return number - ostatok + round;
            }
            else if (number - ostatok == 0)
            {
                return round;
            }
            else return number - ostatok;
        }
        public double FeetToMillimeters(double feet)
        {
            double a = feet;
            return a * 304.8;
        }

        public double MillimetersToFeet(double mm)
        {
            double a = mm;
            return a / 304.8;
        }


        public void CreateIndependentTag(Document document, View view)
        {
            

            TagMode tagMode = TagMode.TM_ADDBY_CATEGORY;
            TagOrientation tagorn = TagOrientation.Horizontal;
            
            //Отсутствует в REVIT 2021
            //TagOrientation tagorn = TagOrientation.AnyModelDirection; 

            FilteredElementCollector fsCollector = new FilteredElementCollector(document, view.Id);
            fsCollector.OfClass(typeof(FamilyInstance)).OfCategory(BuiltInCategory.OST_DetailComponents);
            ICollection<Element> collection = fsCollector.ToElements();
            foreach (FamilyInstance familyInstance in collection.OfType<FamilyInstance>())
            {
                XYZ DetailComponentsLocation = familyInstance.get_BoundingBox(view).Min;
                Reference elRef = new Reference(familyInstance);
                IndependentTag newTag = IndependentTag.Create(document, view.Id, elRef, true, tagMode, tagorn, DetailComponentsLocation);
                if (null == newTag)
                {
                    throw new Exception("Create IndependentTag Failed.");
                }

                newTag.HasLeader = false;
                newTag.TagHeadPosition = DetailComponentsLocation;

                /*
                Parameter foundParameter = element.LookupParameter("Комментарии");
                bool result = foundParameter.Set("Каб.N" +Environment.NewLine + "1");
               */
            }




        }






        public IList<Element> AllElementsOfCategory(Document document, BuiltInCategory category)
        {
            var collector = new FilteredElementCollector(document);
            return collector.OfCategory(category).WhereElementIsNotElementType().ToElements();
        }
        /// <summary>
        /// Выбор элемента
        /// </summary>
        /// <returns></returns>
        static public Element SelectElementId(BuiltInCategory builtInCategory, UIDocument uiDoc, Document doc)
        {
 
            ISelectionFilter selFilter = new MechanicalEquipmentSelectionFilter(builtInCategory);
            Reference myRef = uiDoc.Application.ActiveUIDocument.Selection.PickObject(ObjectType.Element, selFilter, $"{builtInCategory.ToString()}");
            Element elementOfRef = doc.GetElement(myRef);
            return elementOfRef;
        }

        static public List<Element> SelectElementsId(BuiltInCategory builtInCategory, UIDocument uiDoc)
        {
            ISelectionFilter selFilter = new MechanicalEquipmentSelectionFilter(builtInCategory);

            var Elements = uiDoc.Application.ActiveUIDocument.Selection.PickElementsByRectangle(selFilter, $"{builtInCategory.ToString()}");
                      
            return Elements.ToList();
        }


        public void DisConnectDuctCurveWith(MEPSystem mepsystem, Document _doc, BuiltInCategory builtInCategory)
        {

            var faminst = (mepsystem as MechanicalSystem
                ?? throw new ArgumentException("Требуется механическая система.", nameof(mepsystem))).DuctNetwork.OfType<FamilyInstance>();
            foreach (var item in faminst)
            {

                if (GetCategory.GetBuiltInCategory(item.Category) == builtInCategory)
                {
                    foreach (Connector con in item.MEPModel.ConnectorManager.Connectors)
                    {
                        if (con.Domain == Domain.DomainHvac)
                        {
                            Connector FromEquipment = con;
                            foreach (Connector i in con.AllRefs)
                            {
                                Connector FromDuct = i;
                                FromDuct.DisconnectFrom(FromEquipment);

                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Рассоединение парных коннекторов из графа (от воздуховодов)
        /// </summary>
        /// <param name="sys"></param>
        /// <param name="nameofSourse">ADSK_Позиция</param>
        /// <param name="nameofParametr">ADSK_Имя контроллера</param>
        /// <returns></returns>
        public void ConnectDuctCurveWith(MEPSystem mepsystem, Document _doc, BuiltInCategory builtInCategory)
        {
            var ducts = (mepsystem as MechanicalSystem
                ?? throw new ArgumentException("Требуется механическая система.", nameof(mepsystem))).DuctNetwork.OfType<Duct>();

            var collector = new FilteredElementCollector(_doc);
            var listofelements = collector.OfCategory(builtInCategory).WhereElementIsNotElementType().OfType<FamilyInstance>().ToList();


            foreach (var d in ducts)
            {
                foreach (Connector conCurveDuct in (d as MEPCurve).ConnectorManager.Connectors)
                {
                    if (conCurveDuct.IsConnected == false)
                    {
                        foreach (var fi in listofelements)
                        {
                            if (GetCategory.GetBuiltInCategory(fi.Category) == builtInCategory)
                            {
                                foreach (Connector connectorElecticalEq in fi.MEPModel.ConnectorManager.Connectors)
                                {
                                    if (connectorElecticalEq.Domain == Domain.DomainHvac)
                                    {
                                        //if ((conCurveDuct.Origin.X == connectorElecticalEq.Origin.X) && (conCurveDuct.Origin.Y == connectorElecticalEq.Origin.Y) && (conCurveDuct.Origin.Z == connectorElecticalEq.Origin.Z))
                                        if (conCurveDuct.Origin.IsAlmostEqualTo(connectorElecticalEq.Origin))
                                        {
                                            try
                                            {
                                                connectorElecticalEq.ConnectTo(conCurveDuct);
                                            }
                                            catch { continue; }
                                        }
                                    }
                                }
                            }

                        }
                    }
                }
            }

        }


        /// <summary>
        /// is true if PartType may be Tee,Elbow,Transition...or anything else
        /// </summary>
        /// <param name="fi">FamilyInstance</param>
        /// <param name="partType">PartType may be Tee,Elbow,Transition...or anything else </param>
        /// <returns></returns>
        static public bool IsPartType(FamilyInstance fi, PartType partType)
        {
            if ((fi.MEPModel as MechanicalFitting)?.PartType == partType) return true;
            else return false;
        }


        /// <summary>
        /// Get MEPSystems GetElement(elementId) as FamilyInstance).MEPModel.ConnectorManager.Connectors
        /// </summary>
        /// <param name="_doc"></param>
        /// <param name="elementId"></param>
        /// /// <param name="domain"></param>
        /// <returns></returns>
        static public List<MEPSystem> GetMEPSystems(Document doc, ElementId elementId, Domain domain)
        {
            List<MEPSystem> lMepSystem = new List<MEPSystem>();
            if (doc.GetElement(elementId) is not FamilyInstance instance || instance.MEPModel?.ConnectorManager is not ConnectorManager manager)
                return lMepSystem;

            foreach (Connector item in manager.Connectors)
            {
                try
                {
                    if (item.MEPSystem is MEPSystem system && item.Domain == domain)
                    {
                        lMepSystem.Add(system);
                    }
                }
                catch { continue; };

            }

            return lMepSystem;

        }



        /// <summary>
        /// Получение елементов EQ из графа
        /// </summary>
        /// <param name="grah"></param>

        /// <returns></returns>
        public List<EQ> EQipmentFromGraph(GraphId grah)
        {

            List<EQ> elid = new List<EQ>();
            foreach (var item in grah.Vertices)
            {
                if (item.eQ != null)
                {
                    elid.Add(item.eQ);
                }

            }
            // var sorted = from FamilyInstance in fi group FamilyInstance by FamilyInstance.LookupParameter(nameofParametr).AsString() into g select new { Name = g.Key, Count = g.ToList() };
            return elid;
        }





        static public VertexId GetVertexId(GraphId Graf, ElementId element)
        {
            // VertexId vertexId = new VertexId(element);   
            List<VertexId> list = new List<VertexId>();
            for (int i = 0; i < Graf.Vertices.Count; i++)
            {
                if (Graf.Vertices[i].Name.Equals(element))
                {
                    list.Add(Graf.Vertices[i]);
                }
            }
            return list[0];

        }


        public class MechanicalEquipmentSelectionFilter(BuiltInCategory builtInCategory) : ISelectionFilter
        {
            public bool AllowElement(Element element)
            {

                if (GetCategory.GetBuiltInCategory(element.Category) == builtInCategory)
                {
                    return true;
                }
                return false;
            }

            public bool AllowReference(Reference refer, XYZ point)
            {
                return true;
                //return false;
            }
        }


        /// <summary>
        /// Создание чертежного вида
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public ViewDrafting CreateViewDrafting(Document doc, string name, bool activate, UIDocument uiDoc)
        {

            using (Transaction tx = new Transaction(doc, "Создание чертежного вида"))
            {
                tx.Start("Создание чертежного вида");

                FilteredElementCollector collector = new FilteredElementCollector(doc);
                collector.OfClass(typeof(ViewFamilyType));
                ViewFamilyType viewFamilyType = collector.Cast<ViewFamilyType>().First(vft => vft.ViewFamily == ViewFamily.Drafting);
                ViewDrafting view = ViewDrafting.Create(doc, viewFamilyType.Id);
                try
                {
                    view.Name = name;
                    TaskDialog.Show("Создан чертежный вид", $"Имя вида: {name}");
                }
                catch { TaskDialog.Show("Создан чертежный вид", $"Имя вида: {view.Name}"); }
                tx.Commit();
                if (activate)
                {
                    uiDoc.ActiveView = view;
                    
                }

                return view;
            }
        }

        /// <summary>
        /// Создание опорных плоскостей
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="formatGost"></param>
        /// <returns></returns>
        public void CreateReferencePlane(Document doc, ViewDrafting view, FormatGost formatGost)
        {
            double X = 24.4;
            double Y = 16.5;
            XYZ max = new XYZ();
            XYZ min = new XYZ();
            if (formatGost == FormatGost.A2_h) { X = 24.4; Y = 16.5; }

            using (Transaction tx = new Transaction(doc, "Создание опорной плоскости"))
            {
                tx.Start("Границы листа");
                XYZ one = new XYZ(0, 0, -36.925196850);
                XYZ two = new XYZ(0, Y, -36.925196850);
                XYZ three = new XYZ(X, Y, -36.925196850);
                XYZ four = new XYZ(X, 0, -36.925196850);
                XYZ cutVec = new XYZ(0, 0, 1);
                ReferencePlane refPlane1 = doc.Create.NewReferencePlane(one, two, cutVec, view);
                refPlane1.Name = "left";
                ReferencePlane refPlane2 = doc.Create.NewReferencePlane(two, three, cutVec, view);
                refPlane2.Name = "top";
                ReferencePlane refPlane3 = doc.Create.NewReferencePlane(three, four, cutVec, view);
                refPlane3.Name = "right";
                ReferencePlane refPlane4 = doc.Create.NewReferencePlane(four, one, cutVec, view);
                refPlane4.Name = "down";
                tx.Commit();
            }
        }
        public enum FormatGost
        {
            A2_h, A3_h
        }



        public void SetParametr(FamilyInstance familyInstance, string paramName, string paramValue)
        {
           
            foreach (Parameter p in familyInstance.Parameters)
            {
               if(p.Definition.Name == paramName)
                {
                    p.Set(paramValue); break;
                }
                
            }
        }




    }


}
