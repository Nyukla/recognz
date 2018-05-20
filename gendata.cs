using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Emgu;
using Emgu.CV;
using Emgu.CV.Structure;
using System.Threading;

/**/

using System.Xml;
using System.Xml.Serialization;
using System.IO;

using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Emgu.CV.ML;


namespace EmguC
{
    
    public partial class Form1 : Form
    {
        Form2 f2 = new Form2();
        Form3 f3 = new Form3();
        

        Capture capture;                    // захват видео
        System.Windows.Forms.Timer timer;
        string filteredFinalString;         // содержит автомобильный номер

        int minPlWidth ,      // хранят границы региона распознавания для номерного знака
            minPlateHeight,   //
            maxPlateWidth,    // 
            maxPlateHeight;   //    

        int playbackSpeed;    // скорость воспроизведения 
        

        public Form1()
        {
            InitializeComponent();
            button1.Enabled = false;
            button2.Enabled = false;
        }

        private void открытьФайлToolStripMenuItem_Click(object sender, EventArgs e)
        {
            
                OpenFileDialog opf = new OpenFileDialog();
                opf.Filter = "Video Files |*.avi";
                if (opf.ShowDialog() == DialogResult.OK)
                {
                    label1.Text = opf.FileName;
                    button1.Enabled = button2.Enabled = true;
                    capture = new Emgu.CV.Capture(opf.FileName);                    
                    timer1.Enabled = true;

                    Image<Bgr, Byte> frame = capture.QueryFrame().ToImage<Bgr, Byte>();
                    f2.trackBar1.SetRange(1, frame.Height);
                    f2.trackBar2.SetRange(1, frame.Height);
                    f2.trackBar1.Value = frame.Height;
                    f2.trackBar2.Value = 1;
                    trackBar1.SetRange(1, frame.Height);
                    trackBar2.SetRange(1, frame.Height);
                    trackBar1.Value = frame.Height;
                    trackBar2.Value = 1; 
                }
                f2.label7.Width = minPlWidth;
                f2.label7.Height = minPlateHeight;
                f2.label8.Width = maxPlateWidth;
                f2.label8.Height = maxPlateHeight;
                f2.trackBar3.SetRange(1, 100);
                f2.trackBar4.SetRange(1, 100);
                f2.trackBar5.SetRange(1, 200);
                f2.trackBar4.Value = 50;                
                playbackSpeed = f2.trackBar5.Value = 65;
                //загрузка настроек из файла
                try
                {   //чтение айла
                    string[] allText = File.ReadAllLines(f2.fileName);         //чтение всех строк файла в массив строк
                    {     
                        f2.trackBar1.Value = Convert.ToInt32(allText[0]);
                        f2.trackBar2.Value = Convert.ToInt32(allText[1]);                        
                        f2.trackBar3.Value = Convert.ToInt32(allText[2]);
                        f2.trackBar4.Value = Convert.ToInt32(allText[3]);
                        f2.trackBar5.Value = Convert.ToInt32(allText[4]);
                        f2.trackBar6.Value = Convert.ToInt32(allText[5]);
                        f2.trackBar7.Value = Convert.ToInt32(allText[6]);
                        trackBar1.Value = Convert.ToInt32(allText[7]);
                        trackBar2.Value = Convert.ToInt32(allText[8]);
                    }
                }
                catch (FileNotFoundException ex)
                {
                    ShowDialog();
                }    

        } 

        private void timer1_Tick(object sender, EventArgs e)
        {            
            Pocess();            
        }

        private void button1_Click(object sender, EventArgs e)  // старт
        {
            button1.Enabled = false;
            button2.Enabled = true;
            timer = new System.Windows.Forms.Timer();
            timer.Tick += new EventHandler(timer1_Tick);
            timer.Interval = 1;
            timer1.Start();
        }
        private void button2_Click(object sender, EventArgs e)
        {
            button2.Enabled = false;
            button1.Enabled = true;
            timer1.Stop();
        }

        public String ReplaceCharInString(String str, int index, Char newSymb)
        {           
            return str.Remove(index, 1).Insert(index, newSymb.ToString());            
        }


        String faceFileName = "cascade2.xml"; //Каскад детектора

        /////////////////////////////////////////
        //Процедура обработки видео и поиcка регистрационного номера        
        void Pocess()
        {
            Image<Bgr, Byte> frame = capture.QueryFrame().ToImage<Bgr, Byte>();

            // настройка ограничения обработки основного кадра    
            Data.posRoi1 =  trackBar1.Value;
            Data.posRoi2 = trackBar2.Value;
            frame.Draw(new LineSegment2D(new Point(1, frame.Height - Data.posRoi1), new Point(frame.Width, frame.Height - Data.posRoi1)), new Bgr(Color.Tomato), 2);
            frame.Draw(new LineSegment2D(new Point(1, frame.Height - Data.posRoi2), new Point(frame.Width, frame.Height - Data.posRoi2)), new Bgr(Color.Tomato), 2);

            // настройка ограничения обработки изображения номерного знака 
            // минимум
            minPlWidth = Convert.ToInt32(f2.trackBar3.Value * 3.4);
            minPlateHeight = Convert.ToInt32(f2.trackBar3.Value * 1);
            f2.label7.Text = (f2.trackBar3.Value * 3.4).ToString() + "x" + (f2.trackBar3.Value * 1).ToString();
            f2.label7.Width = minPlWidth;
            f2.label7.Height = minPlateHeight;
            // максимум
            maxPlateWidth = Convert.ToInt32(f2.trackBar4.Value * 3.4);
            maxPlateHeight = Convert.ToInt32(f2.trackBar4.Value * 1);
            f2.label8.Text = (f2.trackBar4.Value * 3.4).ToString() + "x" + (f2.trackBar4.Value * 1).ToString();
            f2.label8.Width = maxPlateWidth;
            f2.label8.Height = maxPlateHeight;

            // установка паузы между кадрами
            playbackSpeed = f2.trackBar5.Value;
            f2.label10.Text = f2.trackBar5.Value.ToString();
            Image<Bgr, Byte> resized_to_Rotate = new Image<Bgr, Byte>(300, 100);
            Image<Bgr, Byte> resized_to_KNN = new Image<Bgr, Byte>(300, 100);
            using (CascadeClassifier plate = new CascadeClassifier(faceFileName)) //Каскад
            using (Image<Gray, Byte> gray = frame.Convert<Gray, Byte>()) //Хаар работает с ЧБ изображением
            {
                //Детектируем
                Rectangle[] platesDetected2 = plate.DetectMultiScale(gray, 1.1, 6, new Size(minPlWidth, minPlateHeight), new Size(maxPlateWidth, maxPlateHeight));
                //Выводим всё найденное
                foreach (Rectangle f in platesDetected2)
                    if ((f.Y > frame.Height - Data.posRoi1) && (f.Y < frame.Height - Data.posRoi2))
                {


                    Rectangle roi = new Rectangle(f.X, f.Y, f.Width, f.Height);
                    frame.ROI = roi;
                    Image<Bgr, Byte> cropped_im = frame.Copy();
                    frame.ROI = Rectangle.Empty;

                    pictureBox2.Image = cropped_im.Bitmap;                   

                    Image<Bgr, Byte> to_Rotate = cropped_im.Copy();

                    Image<Bgr, Byte> Rotated = cropped_im.Rotate(findAngle(to_Rotate), new Bgr(0, 0, 0)); // разворот изображения
                    CvInvoke.Resize(Rotated, Rotated, new Size(300, 100), 0, 0, Inter.Linear);  // увеличение изображения                  
                    

                   // CvInvoke.Imshow("Rotated", Rotated);                    
                    f3.pictureBox1.Image = cropped_im.Bitmap;
                    f3.pictureBox5.Image = Rotated.Bitmap;

                    frame.Draw(f, new Bgr(0, 255, 0), 2);

                    KNN(frame, roi, Rotated.Mat);
                    
                    resized_to_KNN.Dispose();
                    resized_to_Rotate.Dispose();
                }
            }            
            pictureBox1.Image = frame.Bitmap;
            Thread.Sleep(playbackSpeed);
            frame.Dispose();
        }
        /************Rotate********************************/

        private double findAngle(Image<Bgr, byte> inp) // нахоидит угол наклона изображения к горизотальной линии
        {
            LineSegment2D[] lines = null;
            Image<Bgr, byte> img = inp;
            CvInvoke.Resize(inp, img, new Size(300, 100), 0, 0, Inter.Linear);
            using (Image<Gray, byte> gray = img.Convert<Gray, Byte>())
            using (Image<Gray, float> sobel = new Image<Gray, float>(img.Size))
            {
                CvInvoke.GaussianBlur(gray, gray, new Size(3, 3), 0, 1, BorderType.Default);
                Image<Bgr, Byte> converted = gray.Convert<Bgr, byte>();

                // вывод изображения номерного знака на форму 3 после сглаживания и конвертирования в оттенки серого
                Image<Bgr, Byte> imgConverted1 = converted.Mat.ToImage<Bgr, Byte>();
                f3.pictureBox2.Image = imgConverted1.Bitmap;
                
                // применение оператора собеля и преобразования хафа для выделения локальных максимумов и построения прямых
                CvInvoke.Sobel(gray, gray, DepthType.Default, 0, 1, 3);
                lines = gray.HoughLinesBinary(1, Math.PI / 180, 100, gray.Width / 2, 11)[0]; // минимальная длина прямой должна быть не меньше ширины изображения номерного знака
                
                // вывод изображения номерного знака на форму 3 применения оператора Собеля
                Image<Bgr, Byte> imgConverted2 = gray.Mat.ToImage<Bgr, Byte>();
                f3.pictureBox3.Image = imgConverted2.Bitmap;
                //CvInvoke.Imshow("gray", gray);
            }

            double angle = 0;
            LineSegment2D avr = new LineSegment2D();
            for (int n = 0; n < lines.Length; n++) // number of lines
            {
                if ((lines[n].Length > 250)     // оставляем только наиболее длинные отрезки
                    && (lines[n].P1.Y < 40)     // координаты которых будут значительно выше середины  
                    && (lines[n].P2.Y < 20))    // изображения номера
                {
                    avr.P1 = new Point(lines[n].P1.X, lines[n].P1.Y);
                    avr.P2 = new Point(lines[n].P2.X, lines[n].P2.Y);
                }
            }

            LineSegment2D horizontal = new LineSegment2D(avr.P1, new Point(avr.P2.X, avr.P1.Y));
            img.Draw(new LineSegment2D(avr.P1, new Point(avr.P2.X, avr.P1.Y)), new Bgr(0, 0, 255), 2);
            img.Draw(avr, new Bgr(0, 255, 0), 2);

            Image<Bgr, Byte> imgConverted3 = img.Mat.ToImage<Bgr, Byte>();
            f3.pictureBox4.Image = imgConverted3.Bitmap;   

            double c = horizontal.P2.X - horizontal.P1.X;
            double a = Math.Abs(horizontal.P2.Y - avr.P2.Y);
            double b = Math.Sqrt(c * c + a * a);
            angle = (a / b * (180 / Math.PI)) * (horizontal.P2.Y > avr.P2.Y ? 1 : -1);
            img.Dispose();
            return angle;
        }

        /************KNN********************************/

        bool detected = false;
        const int RESIZED_IMAGE_WIDTH = 20;
        const int RESIZED_IMAGE_HEIGHT = 30;

        private bool KNN(Image<Bgr, Byte> frm, Rectangle r, Mat plate)  // принимает изображение целиком, прямоугольник для области номерного знака, вырезанное изображение номернго знака
        {
            //чтение XMl производится 2 раза
            //сначала считывается количество строк (которое воспадает с количеством образцов)
            //при первом считывании из XML не извлекаются данные, так как, так как мы не знаем точное количество строк
            //во второй раз заново инициализируем классификационную матрицу и матрицу для тренировочных изображений с корректным( известным ) значением строк
            //почле чего извлекаем данные в обе матрицы

            Matrix<Single> mtxClassifications = new Matrix<Single>(1, 1);       //для начала обозначим, что у матриц будет одна строка и один столбец 
            Matrix<Single> mtxTrainingImages = new Matrix<Single>(1, 1);        //далее будем менять их размер, в соответствии с выясненным количеством строк (то есть. количеством тренировочных образцов)

            var intValidChars = new List<int>(new int[] {
				'0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
				'A', 'B', 'C', 'E', 'H','K', 'M', 'O', 'P', 'T','X', 'Y' });


            XmlSerializer xmlSerializer = new XmlSerializer(mtxClassifications.GetType());          //переменные для сохранения данных
            StreamReader streamReader;                                                              //из xml - файла

            try
            {
                streamReader = new StreamReader("classifications.xml");                         //попытка открыть файл классификатора
            }
            catch (Exception ex)
            {                                                              //вывод сообщения об ошибке, если открыть файл не удалось
                MessageBox.Show("Классификатор classifications.xml не найден", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            //пытаемся десериализовать XML
            mtxClassifications = (Matrix<Single>)xmlSerializer.Deserialize(streamReader);

            streamReader.Close();            //закрываем XML file
            int intNumberOfTrainingSamples = mtxClassifications.Rows; //получаем колчество строк , то есть количество тренировочных образцов

            //повторно инициализируем матрицы с известным количеством строк
            //
            mtxClassifications = new Matrix<Single>(intNumberOfTrainingSamples, 1);
            mtxTrainingImages = new Matrix<Single>(intNumberOfTrainingSamples, RESIZED_IMAGE_WIDTH * RESIZED_IMAGE_HEIGHT);

            try
            {
                streamReader = new StreamReader("classifications.xml");      //повторно инициализируем stream reader, пытаемся открыть классификационный файл снова
            }
            catch (Exception ex)
            {                                                        // если при попытке прочитать файл ошибка -  выводим сообщение о ней и останавливаем программу
                MessageBox.Show("Классификатор classifications.xml не найден", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            //считываем данные из файла - классификатора
            mtxClassifications = (Matrix<Single>)xmlSerializer.Deserialize(streamReader);
            streamReader.Close();               //закрываем XML файл
            xmlSerializer = new XmlSerializer(mtxTrainingImages.GetType());                

            try
            {
                streamReader = new StreamReader("images.xml");          //попытка открыть классификационный файл
            }
            catch (Exception ex)
            {

                MessageBox.Show("Классификатор images.xml не найден", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            mtxTrainingImages = (Matrix<Single>)xmlSerializer.Deserialize(streamReader);       //считывание данных из файла с тренировочными изображениями
            streamReader.Close();                                                              //закрываем его

            // тенеровка
            KNearest kNearest = new KNearest();

            kNearest.DefaultK = 1;

            kNearest.Train(mtxTrainingImages, Emgu.CV.ML.MlEnum.DataLayoutType.RowSample, mtxClassifications);

            // тест

            Mat imgTestingNumbers;              //перемення для работы с изображением

            try
            {
                imgTestingNumbers = plate;      //открываем изображение
            }
            catch (Exception ex)
            {                                                                  //если ошибка
                label1.Text = "unable to open image, error: " + ex.Message;    //показываем сообщение об ошибке
                return false;                                                        //выход
            }

            if (imgTestingNumbers == null)
            {                                                       //если нельзя отерыть изображение
                label1.Text = "unable to open image";               //показываем сообщение об ошибке
                return false;                                             //выход
            }

            if (imgTestingNumbers.IsEmpty)
            {
                label1.Text = "unable to open image";
                return false;
            }

            // морфологические преобразования
            Mat imgGrayscale = new Mat();               //
            Mat imgBlurred = new Mat();                 //
            Mat imgErode = new Mat();
            Mat imgDilat = new Mat();
            Mat imgThresh = new Mat();                  //
            Mat imgThreshCopy = new Mat();              //

            CvInvoke.CvtColor(imgTestingNumbers, imgGrayscale, ColorConversion.Bgr2Gray);        //конвертируем в изображение в оттенки серого
            CvInvoke.GaussianBlur(imgGrayscale, imgBlurred, new Size(5, 5), 0);                  //размытие изображения

            // эрозия и дилатация
            f2.trackBar6.SetRange(1, 20);
            f2.trackBar7.SetRange(1, 20);
            f2.trackBar1.SetRange(1, 20);
            f2.trackBar2.SetRange(1, 20);  

            int kx = f2.trackBar6.Value;
            int ky = f2.trackBar7.Value;
            int kx2 = f2.trackBar1.Value;
            int ky2 = f2.trackBar2.Value;

            Point anchor = new Point(-1, -1);

            Size kSize = new System.Drawing.Size(kx, ky);
            Size kSize2 = new System.Drawing.Size(kx2, ky2);            
            Mat element = CvInvoke.GetStructuringElement(ElementShape.Cross, kSize, anchor);
            Mat element2 = CvInvoke.GetStructuringElement(ElementShape.Cross, kSize2, anchor);
            imgErode = new Mat();
            CvInvoke.Erode(imgBlurred, imgDilat, element2, anchor, 1, BorderType.Default, new MCvScalar(0, 0, 0));
            CvInvoke.Dilate(imgDilat, imgErode, element, anchor, 1, BorderType.Default, new MCvScalar(0, 0, 0));
          

            //вывод на форму
            Image<Bgr, Byte> imgConverted1 = imgErode.ToImage<Bgr, Byte>();
            f3.pictureBox6.Image = imgConverted1.Bitmap;

            //бинаризируем изображение
            CvInvoke.AdaptiveThreshold(imgErode, imgThresh, 255.0, AdaptiveThresholdType.MeanC, ThresholdType.BinaryInv, 11, 2.0);
            //CvInvoke.Imshow("imgTestingNumbers", imgThresh);
           
            //вывод на форму
            Image<Bgr, Byte> imgConverted2 = imgThresh.ToImage<Bgr, Byte>();
            f3.pictureBox7.Image = imgConverted2.Bitmap;

            imgThreshCopy = imgThresh.Clone();          //создаем копию бинаризированного изображения, которая будет использоваться при вызове функции поиска контуров
            VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();

            //находим только внешние контуры
            CvInvoke.FindContours(imgThreshCopy, contours, null, RetrType.External, ChainApproxMethod.ChainApproxSimple);
            List<ContourWithData> listOfContoursWithData = new List<ContourWithData>();          //объявим список контуров

            //заполним список найденными контурами
            if (contours.Size > 5)
                for (int i = 0; i <= contours.Size - 1; ++i)
                {                 //для каждого контура
                    ContourWithData contourWithData = new ContourWithData();                              //обьявим новый контур с данными
                    contourWithData.contour = contours[i];                                                //присвоим ему значение для текущего контура                
                    contourWithData.boundingRect = CvInvoke.BoundingRectangle(contourWithData.contour);   //найдем ограничиваюший прямоугольник для контура

                    if ((contourWithData.boundingRect.Width <= contourWithData.boundingRect.Height)       // ширина ограничивающего прямолугольника не должна быть больше его высоты
                         &&  contourWithData.boundingRect.Height < 2.2 * contourWithData.boundingRect.Width)
                    {
                        contourWithData.dblArea = CvInvoke.ContourArea(contourWithData.contour);          //расчитаем область  
                    }


                    if (contourWithData.checkIfContourIsValid())
                    {                                       
                        listOfContoursWithData.Add(contourWithData);                                       //добавляем текущий контур в список
                    }

                }

            //сортируем контуры с данными слева - направо
            listOfContoursWithData.Sort(
                (oneContourWithData, otherContourWithData) =>
                {
                    return oneContourWithData.boundingRect.X.CompareTo(otherContourWithData.boundingRect.X);
                });

            string strFinalString = "";           //строка, которая будет содержать конечную цепочку символов

            foreach (ContourWithData contourWithData in listOfContoursWithData)
            {// для каждого контура в списке контуров с данными
                CvInvoke.Rectangle(imgTestingNumbers, contourWithData.boundingRect, new MCvScalar(0.0, 255.0, 0.0), 2);      //рисуем прямоугольник вокруг символа
                Mat imgROItoBeCloned = new Mat(imgThresh, contourWithData.boundingRect);        //устанавливаем регион для области ограничивающего прямоугольника
                Mat imgROI = imgROItoBeCloned.Clone();                //клонируем изображение из региона в отдельное изображение
                Mat imgROIResized = new Mat();

                //увеличиваем изображения, что позитивно сказывается на процессе распознавания
                CvInvoke.Resize(imgROI, imgROIResized, new Size(RESIZED_IMAGE_WIDTH, RESIZED_IMAGE_HEIGHT));

                //обьявим матрицу, размер которой совпадает с размером обрабатываемого изоборажения символа
                Matrix<Single> mtxTemp = new Matrix<Single>(imgROIResized.Size);

                //обьявим матрицу с одной строкой , но размером как у обрабатываемого изоборажения символа
                Matrix<Single> mtxTempReshaped = new Matrix<Single>(1, RESIZED_IMAGE_WIDTH * RESIZED_IMAGE_HEIGHT);
                imgROIResized.ConvertTo(mtxTemp, DepthType.Cv32F);          //копинуем данные в матрицу с одной строкой

                for (int intRow = 0; intRow <= RESIZED_IMAGE_HEIGHT - 1; ++intRow)
                {       
                    for (int intCol = 0; intCol <= RESIZED_IMAGE_WIDTH - 1; ++intCol)
                    {
                        mtxTempReshaped[0, (intRow * RESIZED_IMAGE_WIDTH) + intCol] = mtxTemp[intRow, intCol];
                    }
                }

                Single sngCurrentChar;
                sngCurrentChar = kNearest.Predict(mtxTempReshaped);             //вызываем Predict 
                strFinalString = strFinalString + (char)(Convert.ToInt32(sngCurrentChar));         //добавляем символ к строке
            }

            if (strFinalString.Length > 7)                     
                {
                    String ccc = strFinalString;
                    
                    // фильтрация первого символа
                    if (strFinalString[0] == '8') ccc = ReplaceCharInString(strFinalString, 0, 'B');
                    if (strFinalString[0] == '1') ccc = ReplaceCharInString(strFinalString, 0, 'М');

                    // фильтрация 4,5 символов                   
                    for (int i = 4; i <= 5; i++)
                        if (strFinalString[0] == '8') ccc = ReplaceCharInString(strFinalString, 0, 'B');

                    // фильтрация 7-9 символов
                    if (strFinalString.Length == 9)
                    {
                        for (int i = 6; i <= 8; i++)
                            if (strFinalString[i] == 'Y') ccc = ReplaceCharInString(strFinalString, i, '7');
                        for (int i = 6; i <= 8; i++)
                            if (strFinalString[i] == 'T') ccc = ReplaceCharInString(strFinalString, i, '1');
                        for (int i = 6; i <= 8; i++)
                            if (strFinalString[i] == 'C') ccc = ReplaceCharInString(strFinalString, i, '0');
                        for (int i = 6; i <= 8; i++)
                            if (strFinalString[i] == 'O') ccc = ReplaceCharInString(strFinalString, i, '0'); 
                    }

                    if ((strFinalString[0] > 'A') && (strFinalString[0] < 'Y'))
                    //if ((strFinalString[1] > '0') && (strFinalString[1] < '9'))
                    //if ((strFinalString[2] > '0') && (strFinalString[2] < '9'))
                    //if ((strFinalString[3] > '0') && (strFinalString[3] < '9'))
                    {
                        strFinalString = ccc;


                        filteredFinalString = strFinalString;
                        label6.Text = filteredFinalString;

                        listBox1.SelectedIndex = listBox1.Items.Count - 1;
                        listBox1.SelectedIndex = -1;
                        listBox1.Items.Add((filteredFinalString + "\n"));
                        detected = true;

                        LineSegment2D link = new LineSegment2D(new Point(r.X, r.Y + r.Height), new Point(r.X - 2, r.Y + r.Height + 12));
                        LineSegment2D link2 = new LineSegment2D(new Point(r.X + r.Width, r.Y + r.Height), new Point(r.X - 2 + r.Width * 2 - 20, r.Y + r.Height + 12));
                        frm.Draw(link, new Bgr(0, 255, 0), 2);
                        frm.Draw(link2, new Bgr(0, 255, 0), 2);

                        Rectangle additionalR = new Rectangle(r.X - 2, r.Y + r.Height + 12, r.Width * 2 - 20, r.Height);
                        frm.Draw(additionalR, new Bgr(Color.White), -2);
                        CvInvoke.PutText(frm, filteredFinalString, new Point(r.X + 3, r.Y + r.Height + 34), FontFace.HersheySimplex, 0.8, new Bgr(Color.Black).MCvScalar, 2);
                        frm.Draw(r, new Bgr(0, 255, 0), 2);
                    }
                    
                }
            

            //CvInvoke.Imshow("imgTestingw", imgTestingNumbers);
            Image<Bgr, Byte> imgConverted = imgTestingNumbers.ToImage<Bgr, Byte>();
            f3.pictureBox8.Image = imgConverted.Bitmap;
            return detected;

        }

        private void показатьОбработкуИзображенияToolStripMenuItem_Click(object sender, EventArgs e)
        {
            f3.Show();
        }
        

        public class ContourWithData
        {
            const int MIN_CONTOUR_AREA = 100;
            public VectorOfPoint contour; //контур
            public Rectangle boundingRect; //ограничивающий прямоугольник
            public Double dblArea; //область контура

            public bool checkIfContourIsValid()
            { 
                if (dblArea < MIN_CONTOUR_AREA)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
        }    

        private void серединаИзображенияToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        private void установитьРегионРаспознаванияToolStripMenuItem_Click(object sender, EventArgs e)
        {
            f2.Refresh();
            f2.Show();
        }
    }
    static class Data
    {// хранят границы региона распознавания для всего кадра
        public static int posRoi1 { get; set; }
        public static int posRoi2 { get; set; }
    }
}

