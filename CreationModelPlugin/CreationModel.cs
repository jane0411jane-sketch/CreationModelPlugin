using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CreationModelPlugin
{
    [TransactionAttribute(TransactionMode.Manual)]
    public class CreationModel : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;
            Level level1 = GetLevelByName(doc, "Уровень 1");
            Level level2 = GetLevelByName(doc, "Уровень 2");

            List<Wall> walls = CreateWalls(doc, level1, level2);
                    
            
            return Result.Succeeded;
        }

        private void AddRoof(Document doc, Level level2, List<Wall> walls)
        {
            RoofType roofType = new FilteredElementCollector(doc)
                .OfClass(typeof(RoofType))
                .OfType<RoofType>()
                .Where(x => x.Name.Equals("Типовой - 400мм"))
                .Where(x => x.FamilyName.Equals("Базовая крыша"))
                .FirstOrDefault();
            double wallWidth = walls[0].Width;
            double dt = wallWidth / 2;

            LocationCurve curveX = walls[0].Location as LocationCurve;
            XYZ lengthRoofStart = curveX.Curve.GetEndPoint(0);
            XYZ lengthRoofEnd = curveX.Curve.GetEndPoint(1);

            LocationCurve curveY = walls[1].Location as LocationCurve;
            XYZ widthRoofStart = curveY.Curve.GetEndPoint(0);
            XYZ widthRoofEnd = curveY.Curve.GetEndPoint(1);

            double roofHeight = UnitUtils.ConvertToInternalUnits(2000, UnitTypeId.Millimeters);

            ReferencePlane refPlane = doc.Create.NewReferencePlane(
                new XYZ(0, 0, 0), new XYZ(0, 0, 1), new XYZ(0, 1, 0), doc.ActiveView);

            Application application= doc.Application;
            CurveArray profile = application.Create.NewCurveArray();
            XYZ start = new XYZ(0, widthRoofStart.Y - dt, level2.Elevation);
            XYZ mid = new XYZ(0, 0, level2.Elevation + roofHeight);
            XYZ end = new XYZ(0, widthRoofEnd.Y + dt, level2.Elevation);
            profile.Append(Line.CreateBound(start, mid));
            profile.Append(Line.CreateBound(mid, end));

            double extrusionStart = lengthRoofStart.X - dt;
            double extrusionEnd = lengthRoofEnd.X + dt;

            ExtrusionRoof extrusionRoof = doc.Create.NewExtrusionRoof(profile, refPlane, level2, roofType, extrusionStart, extrusionEnd);

        }

        private void AddDoor(Document doc, Level level1, Wall wall)
        {
            FamilySymbol doorType = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Doors)
                .OfType<FamilySymbol>()
                .Where(x=>x.Name.Equals("0762 x 2032 мм"))
                .Where(x => x.FamilyName.Equals("Одиночные-Щитовые"))
                .FirstOrDefault();

            LocationCurve hostCurve = wall.Location as LocationCurve;
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);
            XYZ point = (point1+ point2)/2;
            if(!doorType.IsActive)
                doorType.Activate();    

            doc.Create.NewFamilyInstance(point, doorType,wall,level1,Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
        }

        private List<Wall> CreateWalls(Document doc, Level level1, Level level2)
        {
            double width = UnitUtils.ConvertToInternalUnits(10000, UnitTypeId.Millimeters);
            double depth = UnitUtils.ConvertToInternalUnits(5000, UnitTypeId.Millimeters);
            double dx = width / 2;
            double dy = depth / 2;

            List<XYZ> points = new List<XYZ>();
            points.Add(new XYZ(-dx, -dy, 0));
            points.Add(new XYZ(dx, -dy, 0));
            points.Add(new XYZ(dx, dy, 0));
            points.Add(new XYZ(-dx, dy, 0));
            points.Add(new XYZ(-dx, -dy, 0));

            List<Wall> walls = new List<Wall>();

            Transaction transaction = new Transaction(doc, "Построение стен");
            transaction.Start();
            for (int i = 0; i < 4; i++)
            {
                Line line = Line.CreateBound(points[i], points[i + 1]);
                Wall wall = Wall.Create(doc, line, level1.Id, false);
                walls.Add(wall);
                wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(level2.Id);
            }

            AddDoor(doc, level1, walls[0]);

            for (int i = 1; i < 4; i++)
            {
                AddWindow(doc,level1, walls[i]);
            }

            AddRoof(doc, level2, walls);

            transaction.Commit();

            return walls;

        }

        private void AddWindow(Document doc,Level level1, Wall wall)
        {
            FamilySymbol windowType = new FilteredElementCollector(doc)
               .OfClass(typeof(FamilySymbol))
               .OfCategory(BuiltInCategory.OST_Windows)
               .OfType<FamilySymbol>()
               .Where(x => x.Name.Equals("0915 x 1220 мм"))
               .Where(x => x.FamilyName.Equals("Фиксированные"))
               .FirstOrDefault();
            LocationCurve hostCurve = wall.Location as LocationCurve;
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);
            XYZ point = (point1 + point2) / 2;
            if (!windowType.IsActive)
                windowType.Activate();
            FamilyInstance window = doc.Create.NewFamilyInstance(point, windowType,wall,level1,Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
            window.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM).Set(UnitUtils.ConvertToInternalUnits(800, UnitTypeId.Millimeters));

        }

        private Level GetLevelByName(Document doc, string levelName)
        {
            List<Level> listLevel = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .OfType<Level>()
                .ToList();
            return listLevel
                .Where(x => x.Name.Equals(levelName))
                .FirstOrDefault();            
        }

        

    }
}
