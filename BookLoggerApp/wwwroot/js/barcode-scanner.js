// Barcode Scanner using Quagga.js for ISBN scanning
window.barcodeScanner = {
    dotNetHelper: null,
    isScanning: false,
    videoElement: null,
    stream: null,
    videoTrack: null,

    // Initialize the scanner
    async initialize(dotNetHelper, videoElementId) {
        this.dotNetHelper = dotNetHelper;
        this.videoElement = document.getElementById(videoElementId);

        if (!this.videoElement) {
            console.error('Video element not found:', videoElementId);
            return { success: false, error: 'Video element not found' };
        }

        try {
            console.log('Requesting camera access...');

            // Get list of video devices to find the back camera
            const devices = await navigator.mediaDevices.enumerateDevices();
            const videoDevices = devices.filter(d => d.kind === 'videoinput');
            console.log('Available cameras:', videoDevices.length);

            // Try to find the back camera by label or use facingMode
            let constraints = {
                video: {
                    facingMode: { ideal: 'environment' },
                    width: { min: 640, ideal: 1280, max: 1920 },
                    height: { min: 480, ideal: 720, max: 1080 }
                }
            };

            // Request camera access
            this.stream = await navigator.mediaDevices.getUserMedia(constraints);
            this.videoTrack = this.stream.getVideoTracks()[0];

            console.log('Camera track:', this.videoTrack.label);
            console.log('Track settings:', JSON.stringify(this.videoTrack.getSettings()));

            // Check capabilities after getting stream
            if (this.videoTrack.getCapabilities) {
                const caps = this.videoTrack.getCapabilities();
                console.log('Track capabilities:', JSON.stringify(caps));
            }

            this.videoElement.srcObject = this.stream;
            await this.videoElement.play();

            console.log('Video stream started successfully');

            // Now start Quagga for barcode detection
            this.isScanning = true;
            this.startScanning();

            return { success: true };
        } catch (error) {
            console.error('Camera access error:', error);
            return {
                success: false,
                error: error.message || 'Failed to access camera'
            };
        }
    },

    // Start scanning for barcodes using the canvas overlay technique
    startScanning() {
        if (!Quagga) {
            console.error('Quagga.js not loaded');
            this.dotNetHelper.invokeMethodAsync('OnScanError', 'Quagga.js not loaded');
            return;
        }

        console.log('Starting Quagga scanner with existing video stream...');

        // Create a canvas for Quagga to process
        const canvas = document.createElement('canvas');
        const context = canvas.getContext('2d');

        // Set canvas size to match video
        canvas.width = this.videoElement.videoWidth || 640;
        canvas.height = this.videoElement.videoHeight || 480;

        console.log('Canvas size:', canvas.width, 'x', canvas.height);

        // Process video frames
        const processFrame = () => {
            if (!this.isScanning) return;

            // Draw current video frame to canvas
            context.drawImage(this.videoElement, 0, 0, canvas.width, canvas.height);

            // Use Quagga to decode the frame
            Quagga.decodeSingle({
                decoder: {
                    readers: [
                        "ean_reader",      // EAN-13 (ISBN-13)
                        "ean_8_reader",    // EAN-8
                        "upc_reader",      // UPC-A
                        "upc_e_reader"     // UPC-E
                    ]
                },
                locate: true,
                src: canvas.toDataURL('image/jpeg')
            }, (result) => {
                if (result && result.codeResult && result.codeResult.code) {
                    const code = result.codeResult.code;
                    console.log('Barcode detected:', code, 'Format:', result.codeResult.format);

                    // Validate ISBN format (10 or 13 digits)
                    if (this.isValidIsbn(code)) {
                        console.log('Valid ISBN detected:', code);
                        // Stop scanning after successful detection
                        this.stopScanning();
                        // Notify Blazor component
                        this.dotNetHelper.invokeMethodAsync('OnBarcodeDetected', code);
                        return; // Stop processing
                    } else {
                        console.log('Invalid ISBN format:', code);
                    }
                }

                // Continue processing next frame
                if (this.isScanning) {
                    requestAnimationFrame(processFrame);
                }
            });
        };

        // Wait for video to be ready
        if (this.videoElement.readyState >= 2) {
            console.log('Video ready, starting frame processing');
            processFrame();
        } else {
            console.log('Waiting for video to be ready...');
            this.videoElement.addEventListener('loadeddata', () => {
                console.log('Video loaded, starting frame processing');
                canvas.width = this.videoElement.videoWidth || 640;
                canvas.height = this.videoElement.videoHeight || 480;
                processFrame();
            }, { once: true });
        }
    },

    // Validate ISBN format
    isValidIsbn(code) {
        const cleaned = code.replace(/[-\s]/g, '');
        return cleaned.length === 10 || cleaned.length === 13;
    },

    // Stop scanning and release camera
    stopScanning() {
        console.log('Stopping scanner...');
        this.isScanning = false;

        if (this.stream) {
            this.stream.getTracks().forEach(track => track.stop());
            this.stream = null;
        }

        this.videoTrack = null;

        if (this.videoElement) {
            this.videoElement.srcObject = null;
        }

        console.log('Scanner stopped');
    },

    // Toggle torch/flashlight (if supported)
    async toggleTorch(enable) {
        try {
            if (!this.videoTrack) {
                console.error('No video track available');
                return { success: false, error: 'No active camera' };
            }

            console.log('Attempting to toggle torch:', enable);
            console.log('Track readyState:', this.videoTrack.readyState);

            // Check if torch is in capabilities
            if (this.videoTrack.getCapabilities) {
                const caps = this.videoTrack.getCapabilities();
                console.log('Torch in capabilities:', 'torch' in caps);
            }

            // Try using ImageCapture API (works better on some Android devices)
            if (typeof ImageCapture !== 'undefined') {
                try {
                    const imageCapture = new ImageCapture(this.videoTrack);
                    const photoCapabilities = await imageCapture.getPhotoCapabilities();
                    console.log('Photo capabilities:', JSON.stringify(photoCapabilities));

                    if (photoCapabilities.fillLightMode && photoCapabilities.fillLightMode.includes('flash')) {
                        await this.videoTrack.applyConstraints({
                            advanced: [{ torch: enable }]
                        });
                        console.log('Torch toggled via ImageCapture path:', enable);
                        return { success: true };
                    }
                } catch (imgErr) {
                    console.log('ImageCapture approach failed:', imgErr.message);
                }
            }

            // Direct approach - try to apply torch constraint
            await this.videoTrack.applyConstraints({
                advanced: [{ torch: enable }]
            });

            console.log('Torch toggled:', enable);
            return { success: true };
        } catch (error) {
            console.error('Torch error:', error.name, error.message);

            // Provide more specific error message
            if (error.name === 'NotSupportedError') {
                return { success: false, error: 'Torch not supported by this camera' };
            } else if (error.name === 'OverconstrainedError') {
                return { success: false, error: 'Torch constraint not available' };
            }

            return { success: false, error: 'Torch not available: ' + error.message };
        }
    }
};
