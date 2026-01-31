// Global using directives for common namespaces
// Explicit usings to avoid WPF vs WinForms conflicts

global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.IO;
global using System.Threading.Tasks;

// WPF core
global using System.Windows;
global using System.Windows.Controls;
global using System.Windows.Input;
global using System.Windows.Media;
global using System.Windows.Media.Imaging;

// Type aliases to resolve ambiguous references
// Prefer WPF types for UI components
global using Application = System.Windows.Application;
global using MessageBox = System.Windows.MessageBox;
global using MessageBoxButton = System.Windows.MessageBoxButton;
global using MessageBoxImage = System.Windows.MessageBoxImage;
global using MessageBoxResult = System.Windows.MessageBoxResult;
