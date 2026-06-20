using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Moq;
using CameraViewer.ViewModels;
using CameraViewer.Services;
using CameraViewer.Models;

namespace CameraViewer.Tests.ViewModels
{
    [TestFixture]
    public class MainViewModelTests
    {
        private Mock<OnvifDiscovery> _mockOnvifDiscovery;
        private Mock<V380Discovery> _mockV380Discovery;
        private MainViewModel _viewModel;

        [SetUp]
        public void SetUp()
        {
            _mockOnvifDiscovery = new Mock<OnvifDiscovery>();
            _mockV380Discovery = new Mock<V380Discovery>();
            
            _viewModel = new MainViewModel(
                _mockOnvifDiscovery.Object,
                _mockV380Discovery.Object);
        }

        [TearDown]
        public void TearDown()
        {
            _viewModel?.Dispose();
        }

        [Test]
        public void Constructor_ShouldInitializeProperties()
        {
            // Assert
            Assert.That(_viewModel.DiscoveredCameras, Is.Not.Null);
            Assert.That(_viewModel.DiscoveredCameras.Count, Is.EqualTo(0));
            Assert.That(_viewModel.IsScanning, Is.False);
            Assert.That(_viewModel.ScanButtonText, Is.EqualTo("🔍 Scan Cameras"));
            Assert.That(_viewModel.StatusMessage, Is.EqualTo("Ready to scan"));
        }

        [Test]
        public void Constructor_WithNullOnvifDiscovery_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new MainViewModel(null, _mockV380Discovery.Object));
        }

        [Test]
        public void Constructor_WithNullV380Discovery_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new MainViewModel(_mockOnvifDiscovery.Object, null));
        }

        [Test]
        public void IsScanning_WhenSet_ShouldUpdateScanButtonText()
        {
            // Act
            _viewModel.IsScanning = true;

            // Assert
            Assert.That(_viewModel.ScanButtonText, Is.EqualTo("⏹ Stop Scan"));

            // Act
            _viewModel.IsScanning = false;

            // Assert
            Assert.That(_viewModel.ScanButtonText, Is.EqualTo("🔍 Scan Cameras"));
        }

        [Test]
        public void ScanButtonText_ShouldNotifyPropertyChanged()
        {
            // Arrange
            bool propertyChanged = false;
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_viewModel.ScanButtonText))
                    propertyChanged = true;
            };

            // Act
            _viewModel.ScanButtonText = "Test";

            // Assert
            Assert.That(propertyChanged, Is.True);
        }

        [Test]
        public void StatusMessage_ShouldNotifyPropertyChanged()
        {
            // Arrange
            bool propertyChanged = false;
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_viewModel.StatusMessage))
                    propertyChanged = true;
            };

            // Act
            _viewModel.StatusMessage = "Test message";

            // Assert
            Assert.That(propertyChanged, Is.True);
        }

        [Test]
        public void DiscoveredCameras_ShouldNotifyPropertyChanged()
        {
            // Arrange
            bool propertyChanged = false;
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_viewModel.DiscoveredCameras))
                    propertyChanged = true;
            };

            // Act
            _viewModel.DiscoveredCameras = new System.Collections.ObjectModel.ObservableCollection<CameraInfo>();

            // Assert
            Assert.That(propertyChanged, Is.True);
        }

        [Test]
        public void ScanCamerasCommand_ShouldNotBeNull()
        {
            // Assert
            Assert.That(_viewModel.ScanCamerasCommand, Is.Not.Null);
        }

        [Test]
        public void ScanCamerasCommand_ShouldBeExecutable()
        {
            // Assert
            Assert.That(_viewModel.ScanCamerasCommand.CanExecute(null), Is.True);
        }

        [Test]
        public void Dispose_ShouldNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _viewModel.Dispose());
        }

        [Test]
        public void Dispose_CalledTwice_ShouldNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                _viewModel.Dispose();
                _viewModel.Dispose();
            });
        }
    }
}
