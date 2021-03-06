﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using ABB.Robotics.Controllers;
using ABB.Robotics.Controllers.Discovery;
using ABB.Robotics.Controllers.RapidDomain;
using Autodesk.DesignScript.Geometry;
using DynamoABB;


namespace Dynamo_ABB
{
    public class DynamoABB
    {
        public static Pose RobotPose(Point translation, double q1, double q2, double q3, double q4)
        {
            var r = new Pose();
            r.Trans.X = (float)translation.X;
            r.Trans.Y = (float)translation.Y;
            r.Trans.Z = (float)translation.Z;
            r.Rot.Q1 = (float)q1;
            r.Rot.Q2 = (float)q2;
            r.Rot.Q3 = (float)q3;
            r.Rot.Q4 = (float)q4;
            return r;
        }

        /// <summary>
        /// Set a rapid data object's value in the specified module.
        /// </summary>
        /// <param name="moduleName">The name of the module.</param>
        /// <param name="variableName">The name of the variable.</param>
        /// <param name="variableValue">The value of the variable.</param>
        public static void RobotTargetAtVariableFromString(string moduleName, string variableName, string variableValue)
        {
            try
            {
                var scanner = new NetworkScanner();
                scanner.Scan();

                ControllerInfoCollection controllers = scanner.Controllers;
                using (var controller = ControllerFactory.CreateFrom(controllers[0]))
                {
                    controller.Logon(UserInfo.DefaultUser);
                    using (Mastership.Request(controller.Rapid))
                    {
                        if (controller.OperatingMode == ControllerOperatingMode.Auto)
                        {
                            using (var task = controller.Rapid.GetTask("T_ROB1"))
                            {
                                var target = new RobTarget();
                                using (var rapidData = task.GetRapidData(moduleName, variableName))
                                {
                                    if (rapidData.Value is RobTarget)
                                    {
                                        target.FillFromString2(variableValue);
                                        rapidData.Value = target;
                                    }
                                }
 
                                var result = task.Start(true);
                                Debug.WriteLine(result.ToString());

                                task.ResetProgramPointer();
                            }
                        }
                        else
                        {
                            Debug.WriteLine("Automatic mode is required to start execution from a remote client.");
                        }
                    }
                    controller.Logoff();
                }
                
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                Debug.WriteLine(ex.StackTrace);
            }
        }

        /// <summary>
        /// Create a Robot target from a point.
        /// </summary>
        /// <param name="p">The point.</param>
        /// <returns></returns>
        public static RobTarget TargetAtPoint(Point point)
        {
            var target = new RobTarget();
            if (point != null)
            {
                target.FillFromString2(
                    string.Format(
                        "[[{0},{1},{2}],[0.000000,0.000000,-1.000000,0.000000082],[0,-1,0,1],[9E9,9E9,9E9,9E9,9E9,9E9]];",
                        point.X, point.Y, point.Z));
                //return target;
            }
            return target;

        }

        public static RobTarget TargetAtPointAndOrient(Point point, double q1, double q2, double q3, double q4, double conf1, double conf2, double conf3, double conf4)
        {
            var target = new RobTarget();
            if (point != null)
            {
                target.FillFromString2(
                    string.Format(
                        "[[{0},{1},{2}],[{3},{4},{5},{6}],[0,-1,0,0],[9E9,9E9,9E9,9E9,9E9,9E9]];",
                        point.X, point.Y, point.Z, q1, q2, q3, q4, conf1, conf2, conf3, conf4));
                //return target;
            }
            return target;

        }

        /// <summary>
        /// Create a Robot target from a plane.
        /// </summary>
        /// <param name="p">The point.</param>
        /// <returns></returns>
        public static RobTarget TargetAtPlane(Plane plane)
        {
            var target = new RobTarget();
            if (plane != null)
            {
                List<double> quatDoubles = RobotUtils.PlaneToQuaternian(plane);
                target.FillFromString2(
                    string.Format(
                        "[[{0},{1},{2}],[{3},{4},{5},{6}],[0,-1,0,1],[9E9,9E9,9E9,9E9,9E9,9E9]];",
                        plane.Origin.X, plane.Origin.Y, plane.Origin.Z, quatDoubles[0], quatDoubles[1], quatDoubles[2], quatDoubles[3]));
                //return target;
            }
            return target;

        }
        /// <summary>
        /// Write a Rapid file from a set of targets.
        /// </summary>
        /// <param name="targets">A list of targets.</param>
        /// <param name="filePath">A file path.</param>
        /// <returns></returns>
        public static string WriteRapidFile(List<RobTarget> targets, string filePath, Pose toolPose, Pose workPose, double speed = 100, double zone = 1)
        {
            var targetIds = new List<string>();

            var pathBuilder = new StringBuilder();
            var counter = 0;
            foreach (var target in targets)
            {
                var targetId = "b"+counter;
                targetIds.Add(targetId);
                pathBuilder.AppendLine(string.Format("\tCONST robtarget {0}:={1};", targetId, target));
                counter++;
            }

            var moveBuilder = new StringBuilder();
            foreach (var id in targetIds)
            {
                moveBuilder.AppendLine(string.Format("\t\tMoveL {0},v{1},z{2},tPen\\WObj:=WobjPad;", id, speed, zone));
            }

            using (var tw = new StreamWriter(filePath, false))
            {
                var rapid = string.Format("MODULE MainModule\n"+
                                            string.Format("\tPERS tooldata tPen:=[TRUE,{0},[1,[{1},{2},{3}],[1,0,0,0],0,0,0]];\n", toolPose.ToString(), toolPose.Trans.X/2, toolPose.Trans.Y/2, toolPose.Trans.Z/2) +
                                            string.Format("\tTASK PERS wobjdata WobjPad:=[FALSE,TRUE," + @"""""" + ",{0},[[0,0,0],[1,0,0,0]]];\n", workPose.ToString()) + 
                                            "\t! targets for curve\n"+
                                            "{0}\n"+
                                            "\t! Main routine\n"+
                                            "\tPROC main()\n"+
                                            "\n"+
                                            "\t\tConfL \\Off;\n"+
                                            "\t\tSingArea\\Wrist;\n" +
                                            "\t\trStart;\n"+
                                            "\t\tRETURN;\n"+
                                            "\tENDPROC\n"+
                                            "\tPROC rStart()\n"+
                                            "{1}\n"+
                                            "\t\tRETURN;\n"+
                                            "\tENDPROC\n"+
                                            "ENDMODULE\n",
                pathBuilder.ToString(), moveBuilder.ToString());

                tw.Write(rapid);
                tw.Flush();
            }

            var prfPath = Path.Combine(Path.GetDirectoryName(filePath), "Dynamo.prg");
            using (var tw = new StreamWriter(prfPath, false))
            {
                var prf = "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\n"+
                            "<Program entry=\"main\">\n"+
	                        "<Module>MainModule.mod</Module>\n"+
                            "</Program>\n";

                tw.Write(prf);
                tw.Flush();
            }

            return filePath;
        }

        /// <summary>
        /// Write a Rapid program to your controller and execute it.
        /// This method automatically generates a Dynamo.prg file in the same
        /// directory as the file path argument.
        /// </summary>
        /// <param name="filePath">The path of the file to upload on the local file system.</param>
        /// <returns></returns>
        public static bool WriteProgramToController(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new Exception("File could not be found.");
            }

            var scanner = new NetworkScanner();
            scanner.Scan();

            ControllerInfoCollection controllers = scanner.Controllers;
            using (var controller = ControllerFactory.CreateFrom(controllers[0]))
            {
                controller.Logon(UserInfo.DefaultUser);
                using (Mastership.Request(controller.Rapid))
                {
                    if (controller.OperatingMode == ControllerOperatingMode.Auto)
                    {
                        var fileName = Path.GetFileName(filePath);

                        try
                        {
                            var remoteDir = controller.FileSystem.RemoteDirectory.Replace('/', '\\');
                            var localDir = Path.GetDirectoryName(filePath);
                            controller.FileSystem.LocalDirectory = localDir;

                            //write the mod file
                            controller.FileSystem.PutFile(fileName, true);
                            
                            //write the prf file
                            controller.FileSystem.PutFile("Dynamo.prg", true);

                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex.Message);
                            return false;
                        }
                        
                        using (var task = controller.Rapid.GetTask("T_ROB1")) // note the task name needs to be set up like this in Robot Studio
                        {
                            // read data from file
                            task.LoadProgramFromFile("Dynamo.prg", RapidLoadMode.Replace);
                            task.LoadModuleFromFile(fileName, RapidLoadMode.Add);
                            try
                            {
                                task.ResetProgramPointer();//seems to get hung up here.
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine(ex.Message.ToString());
                                throw;
                            }

                            var result = task.Start(true);
                            Debug.WriteLine(result.ToString());
                        }
                    }
                    else
                    {
                        Debug.WriteLine("Automatic mode is required to start execution from a remote client.");
                    }
                }
                controller.Logoff();
            }

            return true;
        }
    }
}
