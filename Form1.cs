
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

using System.Xml;
using System.Xml.Serialization; //these imports are for writing Matrix objects to file, see end of program
using System.IO;

using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;

namespace EmguC
{
    public partial class Form1 : Form
    {
        const int MIN_CONTOUR_AREA = 100;

        const int RESIZED_IMAGE_WIDTH = 20;
        const int RESIZED_IMAGE_HEIGHT = 30;

        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            DialogResult drChosenFile;
            OpenFileDialog opf = new OpenFileDialog(); 
            //drChosenFile = ofdOpenFile.ShowDialog(); // open file dialog

           /* if (drChosenFile != DialogResult.OK || ofdOpenFile.FileName == "")
            { // if user chose Cancel or filename is blank . . .
                lblChosenFile.Text = "file not chosen"; // show error message on label
                return; // and exit function
            }*/

            Mat imgTrainingNumbers;

            try
            {
                imgTrainingNumbers = CvInvoke.Imread("E:\\Emgu\\EmguC\\EmguC\\training_chars.png", LoadImageType.AnyColor);
            }
            catch (Exception ex)
            { // if error occurred
                label1.Text = "unable to open image, error: " + ex.Message; // show error message on label
                return; // and exit function
            }

            if (imgTrainingNumbers == null)
            { // if image could not be opened
                label1.Text = "unable to open image"; // show error message on label
                return; // and exit function
            }

            label1.Text = opf.FileName; //update label with file name

            Mat imgGrayscale = new Mat();
            Mat imgBlurred = new Mat(); // declare various images
            Mat imgThresh = new Mat();
            Mat imgThreshCopy = new Mat();
            VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
            //Matrix<Single> mtxClassifications = new Matrix<Single>();
            //Matrix<Single> mtxTrainingImages = new Matrix<Single>();
            Mat matTrainingImagesAsFlattenedFloats = new Mat();

            //possible chars we are interested in are digits 0 through 9 and capital letters A through Z, put these in list intValidChars
            var intValidChars = new List<int>(new int[] {
				'0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
				'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J',
				'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T',
				'U', 'V', 'W', 'X', 'Y', 'Z' });

            CvInvoke.CvtColor(imgTrainingNumbers, imgGrayscale, ColorConversion.Bgr2Gray);       //convert to grayscale
            CvInvoke.GaussianBlur(imgGrayscale, imgBlurred, new Size(5, 5), 0);                  //blur

            //threshold image from grayscale to black and white
            CvInvoke.AdaptiveThreshold(imgBlurred, imgThresh, 255.0, AdaptiveThresholdType.GaussianC, ThresholdType.BinaryInv, 11, 2);
            CvInvoke.Imshow("imgThresh", imgThresh);                //show threshold image for reference
            imgThreshCopy = imgThresh.Clone();              //make a copy of the thresh image, this in necessary b/c findContours modifies the image

            //get external countours only
            CvInvoke.FindContours(imgThreshCopy, contours, null, RetrType.External, ChainApproxMethod.ChainApproxSimple);
            int intNumberOfTrainingSamples = contours.Size;
            Matrix<Single> mtxClassifications = new Matrix<Single>(intNumberOfTrainingSamples, 1);      //this is our classifications data structure

            //this is our training images data structure, note we will have to perform some conversions to write to this later
            Matrix<Single> mtxTrainingImages = new Matrix<Single>(intNumberOfTrainingSamples, RESIZED_IMAGE_WIDTH * RESIZED_IMAGE_HEIGHT);

            //this keeps track of which row we are on in both classifications and training images,
            int intTrainingDataRowToAdd = 0;          //note that each sample will correspond to one row in

            //both the classifications XML file and the training images XML file
            for (int i = 0; i <= contours.Size - 1; ++i)
            {                               //for each contour
                if (CvInvoke.ContourArea(contours[i]) > MIN_CONTOUR_AREA)
                {                      //if contour is big enough to consider
                    Rectangle boundingRect = CvInvoke.BoundingRectangle(contours[i]);                //get the bounding rect
                    CvInvoke.Rectangle(imgTrainingNumbers, boundingRect, new MCvScalar(0.0, 0.0, 255.0), 2);    //draw red rectangle around each contour as we ask user for input

                    Mat imgROItoBeCloned = new Mat(imgThresh, boundingRect);        //get ROI image of current char
                    Mat imgROI = imgROItoBeCloned.Clone();           //make a copy so we do not change the ROI area of the original image
                    Mat imgROIResized = new Mat();

                    //resize image, this is necessary for recognition and storage
                    CvInvoke.Resize(imgROI, imgROIResized, new Size(RESIZED_IMAGE_WIDTH, RESIZED_IMAGE_HEIGHT));

                    CvInvoke.Imshow("imgROI", imgROI);                                   //show ROI image for reference
                    CvInvoke.Imshow("imgROIResized", imgROIResized);                     //show resized ROI image for reference
                    CvInvoke.Imshow("imgTrainingNumbers", imgTrainingNumbers);           //show training numbers image, this will now have red rectangles drawn on it

                    int intChar = CvInvoke.WaitKey(0); //get key press

                    if (intChar == 27)
                    { //if esc key was pressed
                        CvInvoke.DestroyAllWindows();
                        return; //exit the function
                    }
                    else if (intValidChars.Contains(intChar))
                    { //else if the char is in the list of chars we are looking for . . .
                        mtxClassifications[intTrainingDataRowToAdd, 0] = Convert.ToSingle(intChar); //write classification char to classifications Matrix

                        //now add the training image (some conversion is necessary first) . . .
                        //note that we have to covert the images to Matrix(Of Single) type, this is necessary to pass into the KNearest object call to train
                        Matrix<Single> mtxTemp = new Matrix<Single>(imgROIResized.Size);
                        Matrix<Single> mtxTempReshaped = new Matrix<Single>(1, RESIZED_IMAGE_WIDTH * RESIZED_IMAGE_HEIGHT);
                        imgROIResized.ConvertTo(mtxTemp, DepthType.Cv32F);           //convert Image to a Matrix of Singles with the same dimensions

                        for (int intRow = 0; intRow <= RESIZED_IMAGE_HEIGHT - 1; ++intRow)
                        {          //flatten Matrix into one row by RESIZED_IMAGE_WIDTH * RESIZED_IMAGE_HEIGHT number of columns
                            for (int intCol = 0; intCol <= RESIZED_IMAGE_WIDTH - 1; ++intCol)
                            {
                                mtxTempReshaped[0, (intRow * RESIZED_IMAGE_WIDTH) + intCol] = mtxTemp[intRow, intCol];
                            }
                        }

                        for (int intCol = 0; intCol <= (RESIZED_IMAGE_WIDTH * RESIZED_IMAGE_HEIGHT) - 1; ++intCol)
                        {         //write flattened Matrix into one row of training images Matrix
                            mtxTrainingImages[intTrainingDataRowToAdd, intCol] = mtxTempReshaped[0, intCol];
                        }
                        intTrainingDataRowToAdd = intTrainingDataRowToAdd + 1; //increment which row, i.e. sample we are on
                    }
                }
            }

            label1.Text = label1.Text + "training complete !!" + "\n" + "\n";

            //save classifications to file
            XmlSerializer xmlSerializer = new XmlSerializer(mtxClassifications.GetType());
            StreamWriter streamWriter;

            try
            {
                streamWriter = new StreamWriter("classifications.xml"); //attempt to open classifications file
            }
            catch (Exception ex)
            {  //if error is encountered, show error and return
                label1.Text = "\n" + label1.Text + "unable to open 'classifications.xml', error:" + "\n";
                label1.Text = label1.Text + ex.Message + "\n" + "\n";
                return;
            }

            xmlSerializer.Serialize(streamWriter, mtxClassifications);
            streamWriter.Close();

            //save training images to file
            xmlSerializer = new XmlSerializer(mtxTrainingImages.GetType());

            try
            {
                streamWriter = new StreamWriter("images.xml"); // attempt to open images file
            }
            catch (Exception ex)
            { // if error is encountered, show error and return
                label1.Text = "\n" + label1.Text + "unable to open 'images.xml', error:" + "\n";
                label1.Text = label1.Text + ex.Message + "\n" + "\n";
                return;
            }

            xmlSerializer.Serialize(streamWriter, mtxTrainingImages);
            streamWriter.Close();
            label1.Text = "\n" + label1.Text + "file writing done" + "\n";
            MessageBox.Show("Training complete, file writing done !!");
        }
    }
}