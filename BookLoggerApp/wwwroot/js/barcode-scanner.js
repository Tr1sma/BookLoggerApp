// Barcode Scanner using Quagga.js for ISBN scanning
window.barcodeScanner = {
    dotNetHelper: null,
    isScanning: false,
    videoElement: null,
    stream: null,

    // Initialize the scanner
    async initialize(dotNetHelper, videoElementId) {
        this.dotNetHelper = dotNetHelper;
        this.videoElement = document.getElementById(videoElementId);

        if (!this.videoElement) {
            console.error('Video element not found:', videoElementId);
            return { success: false, error: 'Video element not found' };
        }

        try {
            // Let Quagga handle the video stream
            this.isScanning = true;
            this.startScanning();

            return { success: true };
        } catch (error) {
            console.error('Scanner initialization error:', error);
            return {
                success: false,
                error: error.message || 'Failed to initialize scanner'
            };
        }
    },

    // Start scanning for barcodes
    startScanning() {
        if (!Quagga) {
            console.error('Quagga.js not loaded');
            this.dotNetHelper.invokeMethodAsync('OnScanError', 'Quagga.js not loaded');
            return;
        }

        console.log('Starting Quagga scanner...');

        Quagga.init({
            inputStream: {
                name: "Live",
                type: "LiveStream",
                target: this.videoElement,
                constraints: {
                    facingMode: "environment",
                    width: { min: 640, ideal: 1280, max: 1920 },
                    height: { min: 480, ideal: 720, max: 1080 }
                }
            },
            decoder: {
                readers: [
                    "ean_reader",      // EAN-13 (ISBN-13)
                    "ean_8_reader",    // EAN-8
                    "upc_reader",      // UPC-A
                    "upc_e_reader"     // UPC-E
                ],
                debug: {
                    drawBoundingBox: true,
                    showFrequency: true,
                    drawScanline: true,
                    showPattern: true
                }
            },
            locate: true,
            locator: {
                patchSize: "medium",
                halfSample: true
            },
            numOfWorkers: navigator.hardwareConcurrency || 4,
            frequency: 10
        }, (err) => {
            if (err) {
                console.error('Quagga initialization error:', err);
                this.dotNetHelper.invokeMethodAsync('OnScanError', err.message || 'Failed to initialize scanner');
                return;
            }
            console.log('Quagga initialized successfully');
            Quagga.start();
            console.log('Quagga started');
        });

        // Listen for detected barcodes
        Quagga.onDetected((result) => {
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
                } else {
                    console.log('Invalid ISBN format:', code);
                }
            }
        });

        // Listen for processing events (for debugging)
        Quagga.onProcessed((result) => {
            if (result && result.boxes) {
                console.log('Processing frame, boxes found:', result.boxes.length);
            }
        });
    },

    // Validate ISBN format
    isValidIsbn(code) {
        const cleaned = code.replace(/[-\s]/g, '');
        return cleaned.length === 10 || cleaned.length === 13;
    },

    // Stop scanning and release camera
    stopScanning() {
        this.isScanning = false;

        if (Quagga) {
            Quagga.stop();
            Quagga.offDetected();
        }

        if (this.videoElement) {
            this.videoElement.srcObject = null;
        }

        this.stream = null;
    },

    // Toggle torch/flashlight (if supported)
    async toggleTorch(enable) {
        try {
            // Get the stream from Quagga
            const stream = Quagga.CameraAccess.getActiveStreamLabel();
            if (stream) {
                const videoTrack = Quagga.CameraAccess.getActiveTrack();
                if (videoTrack) {
                    const capabilities = videoTrack.getCapabilities();

                    if ('torch' in capabilities) {
                        await videoTrack.applyConstraints({
                            advanced: [{ torch: enable }]
                        });
                        return { success: true };
                    } else {
                        return { success: false, error: 'Torch not supported' };
                    }
                }
            }
            return { success: false, error: 'No active camera' };
        } catch (error) {
            return { success: false, error: error.message };
        }
    }
};
