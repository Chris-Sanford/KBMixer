namespace RawGlobalInput
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        /// 
        // 'private' is an access modifier. It means that the 'components' field is only accessible within the 'Form1' class. 
        // This encapsulation helps in protecting the data from being accessed or modified from outside the class.

        // 'System.ComponentModel' is a namespace. Namespaces are used to organize code and avoid naming conflicts. 
        // 'ComponentModel' is a part of the .NET framework that provides classes for implementing the run-time and design-time behavior of components and controls.

        // 'IContainer' is an interface within the 'System.ComponentModel' namespace. 
        // It defines methods for managing a collection of components. 
        // An interface in C# is a contract that defines a set of methods and properties that the implementing class must provide.

        // 'components' is the name of the field. 
        // It is a variable that will hold a reference to an object that implements the 'IContainer' interface.

        // '=' is the assignment operator. 
        // It is used to assign a value to a variable. In this case, it assigns 'null' to the 'components' field.

        // 'null' is a literal that represents a null reference, which means that the 'components' field currently does not reference any object.

        // 'components' is initialized to 'null' to ensure it has a defined value and avoid uninitialized variable issues.
        // This allows safe disposal of 'components' in the Dispose method, preventing NullReferenceException if 'components' is not assigned.

        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            richTextBox1 = new RichTextBox();
            SuspendLayout();
            // 
            // richTextBox1
            // 
            richTextBox1.Location = new Point(12, 12);
            richTextBox1.Name = "richTextBox1";
            richTextBox1.Size = new Size(632, 462);
            richTextBox1.TabIndex = 0;
            richTextBox1.Text = "";
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(656, 486);
            Controls.Add(richTextBox1);
            Name = "Form1";
            Text = "Form1";
            ResumeLayout(false);
        }

        #endregion

        // in this case, the TYPE that this 
        // private is an access modifier, in this case declaring that its access is limited to the Form1 type
        // RichTextBox is a type (class) that represents a Windows rich text box control, native to .NET framework
        // richTextBox1 is the name of the field
        // this is partially responsible for ensuring that the rich text box control is displayed on the form
        private RichTextBox richTextBox1;
    }
}
