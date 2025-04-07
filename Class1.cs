using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Electrical;

namespace EqualizeElements
{
    [Transaction(TransactionMode.Manual)]
    public class EqualizeElementsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                // Prompt the user to select elements
                IList<Reference> selectedRefs = uiDoc.Selection.PickObjects(ObjectType.Element, new MEPSelectionFilter(), "Select two or more pipes, ducts, or conduits");

                if (selectedRefs.Count < 2)
                {
                    TaskDialog.Show("Error", "Please select at least two elements.");
                    return Result.Failed;
                }

                // Retrieve selected elements
                List<Element> mepElements = selectedRefs
                    .Select(r => doc.GetElement(r.ElementId))
                    .Where(e => IsSupportedMEPElement(e))
                    .ToList();

                if (mepElements.Count < 2)
                {
                    TaskDialog.Show("Error", "Selected elements must be pipes, ducts, or conduits.");
                    return Result.Failed;
                }

                // Check if elements are parallel
                if (!AreElementsParallel(mepElements))
                {
                    TaskDialog.Show("Error", "Selected elements are not parallel.");
                    return Result.Failed;
                }

                // Get distance from user
                double distance = GetDistanceFromUser();
                if (distance <= 0)
                {
                    TaskDialog.Show("Error", "Invalid distance entered.");
                    return Result.Failed;
                }

                // Convert distance to Revit internal units (feet)
                distance = UnitUtils.ConvertToInternalUnits(distance, UnitTypeId.Meters);

                // Equalize distances
                using (Transaction trans = new Transaction(doc, "Equalize MEP Distances"))
                {
                    trans.Start();
                    Element referenceElement = mepElements[0]; // First element as the reference

                    for (int i = 1; i < mepElements.Count; i++)
                    {
                        MoveElementToEqualizeDistance(referenceElement, mepElements[i], i * distance, doc);
                    }

                    trans.Commit();
                }

                TaskDialog.Show("Success", "Distances between elements have been equalized.");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }

        private bool IsSupportedMEPElement(Element element)
        {
            return element is Pipe || element is Duct || element is Conduit;
        }

        


        private bool AreElementsParallel(List<Element> elements)
        {
            if (elements.Count < 2) return false;

            LocationCurve firstCurve = elements[0].Location as LocationCurve;
            XYZ firstDirection = (firstCurve.Curve.GetEndPoint(1) - firstCurve.Curve.GetEndPoint(0)).Normalize();

            foreach (var element in elements.Skip(1))
            {
                LocationCurve curve = element.Location as LocationCurve;
                XYZ direction = (curve.Curve.GetEndPoint(1) - curve.Curve.GetEndPoint(0)).Normalize();

                if (!firstDirection.IsAlmostEqualTo(direction) && !firstDirection.IsAlmostEqualTo(-direction))
                {
                    return false;
                }
            }

            return true;
        }

        private double GetDistanceFromUser()
        {
            string distanceInput = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter the distance between elements (in meters):",
                "Distance Input",
                "1");

            if (double.TryParse(distanceInput, out double distance))
            {
                return distance;
            }

            return -1;
        }

        private void MoveElementToEqualizeDistance(Element referenceElement, Element currentElement, double targetDistance, Document doc)
        {
            LocationCurve referenceCurve = referenceElement.Location as LocationCurve;
            LocationCurve currentCurve = currentElement.Location as LocationCurve;

            if (referenceCurve == null || currentCurve == null)
                throw new InvalidOperationException("Location curves could not be retrieved.");

            // Get the reference element's midpoint
            XYZ referenceMid = (referenceCurve.Curve.GetEndPoint(0) + referenceCurve.Curve.GetEndPoint(1)) / 2;

            // Get the current element's midpoint
            XYZ currentMid = (currentCurve.Curve.GetEndPoint(0) + currentCurve.Curve.GetEndPoint(1)) / 2;

            // Calculate the direction vector perpendicular to the elements' orientation
            XYZ elementDirection = (referenceCurve.Curve.GetEndPoint(1) - referenceCurve.Curve.GetEndPoint(0)).Normalize();
            XYZ perpendicularDirection = new XYZ(-elementDirection.Y, elementDirection.X, elementDirection.Z);

            // Calculate the target position for the current element
            XYZ targetMid = referenceMid + (perpendicularDirection * targetDistance);

            // Calculate the translation vector
            XYZ translation = targetMid - currentMid;

            // Move the current element
            ElementTransformUtils.MoveElement(doc, currentElement.Id, translation);
        }

        public class MEPSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                return elem is Pipe || elem is Duct || elem is Conduit;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return false;
            }
        }
    }
}
