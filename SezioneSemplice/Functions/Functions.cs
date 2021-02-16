using System.Runtime.InteropServices;
using System.Collections.Generic;
using System;

using STA.Geometria;

namespace STA.Geometria.Masse.Functions
{
    public class Functions
    {
        /// <summary>
        /// Calcola l'area
        /// </summary>
        public static double Area2D(List<Punto2D> VerticiIn)
        {
            double sArea = 0;

            ///Verifica se i punti sono almeno 3
            if (VerticiIn.Count > 2)
            {
                for (int i = 1; i < VerticiIn.Count; i++)
                {
                    sArea += AreaSottesa(VerticiIn[i-1], VerticiIn[i]);
                }

                sArea += AreaSottesa(VerticiIn[VerticiIn.Count-1], VerticiIn[0]);
            }
            return sArea;
        }


        /// <summary>
        /// Metodo per il calcolo dell'area sottesa
        /// </summary>
        /// <param name="punto1">punto iniziale</param>
        /// <param name="punto2">punto finale</param>
        /// <returns>area sottesa</returns>
        public static double AreaSottesa(Punto2D punto1, Punto2D punto2)
        {
            double sAreaSottesa = 0;

            sAreaSottesa = (punto2.X - punto1.X) * (punto2.Y + punto1.Y) / 2;

            return sAreaSottesa;
        }

        /// <summary>
        /// Calcola il baricentro della sezione
        /// </summary>
        public static Punto2D calcolaBaricentro(List<Punto2D> VerticiIn)
        {
            double sx = 0.0;
            double sy = 0.0;
            for (int i = 2; i <= VerticiIn.Count; i++)
            {
                double a1 = (VerticiIn[i].X - VerticiIn[i - 1].X) * VerticiIn[i - 1].Y;
                double b1 = (VerticiIn[i - 1].Y - VerticiIn[i].Y) * VerticiIn[i].X;

                double a2 = (VerticiIn[i].X - VerticiIn[i - 1].X) * (VerticiIn[i].Y - VerticiIn[i - 1].Y) / 2.0;
                double b2 = (VerticiIn[i - 1].Y - VerticiIn[i].Y) * (VerticiIn[i].X - VerticiIn[i].X) / 2.0;

                double yg1 = VerticiIn[i - 1].Y / 2.0;
                double xg1 = VerticiIn[i - 1].X / 2.0;
                double yg2 = VerticiIn[i - 1].Y + (VerticiIn[i].Y - VerticiIn[i - 1].Y) / 3.0;
                double xg2 = VerticiIn[i - 1].X + (VerticiIn[i].X - VerticiIn[i - 1].X) / 3.0;
                sx += a1 * yg1 + a2 * yg2;
                sy += b1 * xg1 + b2 * xg2;
            }
            //double at = area(ref figura);
            return new Punto2D { ID = "Baricentro", X = sx / Area2D(VerticiIn), Y = sy / Area2D(VerticiIn) };

        }

        #region  Con Fortran
        public static double FortranArea2D(List<Punto2D> VerticiIn)
        {
            double Area = 0;

            int numeroVertici = VerticiIn.Count;

            double[] CoordinateX = new double[VerticiIn.Count];
            double[] CoordinateY = new double[VerticiIn.Count];

            int i = 0;
            foreach (Punto2D punto in VerticiIn)
            {
                CoordinateX[i] = punto.X;
                CoordinateY[i] = punto.Y;
                i++;
            }

            polygon_area_2d(ref numeroVertici, ref CoordinateX, ref CoordinateY, ref Area);

            return Area;
        }

        //public static Punto2D ForTranBaricentro2D(List<Punto2D> VerticiIn)
        //{


                
        //    int NumeroVertici = VerticiIn.Count;
        //    double bx, by;

        //    unsafe
        //    {
        //    double* xg, yg;
        //    float*[] CoordinateX = new float*[VerticiIn.Count];

        //    float*[] CoordinateY = new float*[VerticiIn.Count];


        //    int i = 0;
        //    foreach (Punto2D punto in VerticiIn)
        //    {
        //        float* px = Convert.ToSingle(punto.X);
        //        float* py = Convert.ToSingle(punto.Y);
        //        CoordinateX[i] = px;
        //        CoordinateY[i] = py;
        //        i++;
        //    }



        //    int* pNumVert = (int*)NumeroVertici;
        //        FTDLL.POLYGON_CENTROID_2D(pNumVert, CoordinateX, CoordinateY, xg, yg);
        //        return new Punto2D { ID = "Baricentro", X = xg, Y = yg };
        //    }
            
            
        //}


        [DllImport("ForTranGeometria.dll")]
        private static extern void polygon_area_2d(ref int numPunti, ref double[] coordinateX, ref double[] corrdinateY, ref double areaOut);
        #endregion
    }


}
