#include <Adafruit_NeoPixel.h>
#include <SD.h>
#include <TMRpcm.h>

#define PIN 2 // button pin
#define NEOPIXEL_PIN 3 // neopixel pin
#define NUM_PIXELS 12 // number of neopixels
#define SPEAKER_PIN 4 // speaker pin
#define SD_CS_PIN 10 // SD card CS pin
#define RECORDING_TIME_LIMIT 60 // recording time limit in seconds
#define LIGHT_INTENSITY_REDUCE_STEP 25 // neopixel light intensity reduction step
#define MAX_PLAYBACK_COUNT 4 // maximum number of times the recording can be played
#define AUTO_DELETE_TIMER 7 * 24 * 60 * 60 * 1000 // 7 days in milliseconds

Adafruit_NeoPixel neopixels = Adafruit_NeoPixel(NUM_PIXELS, NEOPIXEL_PIN, NEO_GRB + NEO_KHZ800);
TMRpcm tmrpcm;
File recording_file;

uint32_t button_press_time = 0;
uint32_t recording_start_time = 0;
uint32_t playback_start_time = 0;
uint32_t auto_delete_timer = 0;
uint8_t button_press_count = 0;
uint8_t light_intensity = 0;
uint8_t playback_count = 0;
bool recording_active = false;
bool playback_active = false;

void setup() {
  pinMode(PIN, INPUT_PULLUP);
  neopixels.begin();
  tmrpcm.speakerPin = SPEAKER_PIN;
  SD.begin(SD_CS_PIN);
}

void loop() {
  // check for button press
  if (digitalRead(PIN) == LOW) {
    if (button_press_time == 0) {
      button_press_time = millis();
    } else if (millis() - button_press_time >= 3000) { // button held for 3 seconds
      button_press_time = 0;
      neopixels.setPixelColor(0, 255, 255, 255); // set first neopixel to white
      neopixels.show();
      tmrpcm.play("power_on.wav"); // play power on sound
      delay(1000);
      neopixels.setPixelColor(0, 0, 0, 0);
      neopixels.show();
      tmrpcm.play("now_its_on.wav"); // play "now it's on" sound
    } else if (recording_active && millis() - recording_start_time >= 5000) { // mic switch on
      tmrpcm.play("please_mention_your_name.wav"); // play "please mention your name" sound
      delay(5000);
      if (tmrpcm.isPlaying()) {
        tmrpcm.stopPlayback();
      }
      recording_start_time = millis();
      recording_file = SD.open("recording.wav", FILE_WRITE);
      tmrpcm.startRecording(recording_file);
      light_intensity = 0;
      neopixels.setPixelColor(0, 255, 0, 0); // set first neopixel to red
      neopixels.show();
    }
  } else {
    if (button_press_time > 0) {
      if (millis() - button_press_time >= 3000) { // button held for more than 3 seconds
        button_press_time = 0;
        recording_active = true;
      } else { // button pressed for less than 3 seconds
        button_press_count++;
        if (button_press_count == 1) { // first press
          if (!playback_active) { // if not playing back recording
      // if recording is active
	if (recording_active) {
    	if (millis() - recording_start_time >= RECORDING_TIME_LIMIT * 1000) { // recording time limit reached
	      recording_active = false;
	      tmrpcm.stopRecording();
	      recording_file.close();
	      neopixels.setPixelColor(0, 0, 0, 0);
	      neopixels.show();
	      tmrpcm.play("recording_saved.wav"); // play "recording saved" sound
	      delay(2000);
	      auto_delete_timer = millis();
	      button_press_count = 0;
	      playback_count = 0;
	    } else { // recording in progress
	      light_intensity = min(light_intensity + LIGHT_INTENSITY_REDUCE_STEP, 255);
	      neopixels.setPixelColor(0, light_intensity, 0, 0);
	      neopixels.show();
	    }
	  }

  // if playback is active
  if (playback_active) {
    if (!tmrpcm.isPlaying()) { // playback finished
      playback_active = false;
      playback_count++;
      if (playback_count == MAX_PLAYBACK_COUNT) { // maximum playback count reached
        neopixels.setPixelColor(0, 0, 0, 0);
        neopixels.show();
      }
    }
  }
}
   } else { // if playback count is already at maximum
        tmrpcm.play("max_playback_count_reached.wav"); // play "maximum playback count reached" sound
        delay(2000);
      }
    }
  }
  
  // adjust neopixel light intensity
  if (recording_active) {
    light_intensity = min(light_intensity + LIGHT_INTENSITY_REDUCE_STEP, 255);
    for (int i = 1; i < NUM_PIXELS; i++) {
      neopixels.setPixelColor(i, neopixels.getPixelColor(i) / 2);
    }
    neopixels.setPixelColor(0, neopixels.Color(light_intensity, 0, 0));
    neopixels.show();
  } else if (playback_active) {
    light_intensity = min(light_intensity + LIGHT_INTENSITY_REDUCE_STEP, 255);
    for (int i = 1; i < NUM_PIXELS; i++) {
      neopixels.setPixelColor(i, neopixels.getPixelColor(i) / 2);
    }
    neopixels.setPixelColor(0, neopixels.Color(0, light_intensity, 0));
    neopixels.show();
  } else {
    neopixels.setPixelColor(0, 0, 0, 0);
    for (int i = 1; i < NUM_PIXELS; i++) {
      neopixels.setPixelColor(i, neopixels.Color(light_intensity, light_intensity, light_intensity));
    }
    neopixels.show();
  }
  
  // check for recording time limit
  if (recording_active && millis() - recording_start_time >= RECORDING_TIME_LIMIT * 1000) {
    recording_active = false;
    recording_start_time = 0;
    tmrpcm.stopRecording();
    recording_file.close();
    neopixels.setPixelColor(0, 0, 255, 0); // set first neopixel to green
    neopixels.show();
    tmrpcm.play("recording_stopped.wav"); // play "recording stopped" sound
    delay(2000);
    if (SD.exists("recording.wav")) {
      auto_delete_timer = millis();
    }
  }
  
  // check for playback end
  if (playback_active && !tmrpcm.isPlaying()) {
    playback_active = false;
    playback_start_time = 0;
    playback_count++;
    neopixels.setPixelColor(0, 0, 255, 0); // set first neopixel to green
    neopixels.show();
    delay(500);
    neopixels.setPixelColor(0, 0, 0, 0);
    neopixels.show();
    delay(500);
    neopixels.setPixelColor(0, 0, 255, 0);
    neopixels.show();
    if (playback_count >= MAX_PLAYBACK_COUNT) {
      if (SD.exists("recording.wav")) {
        auto_delete_timer = millis();
      }
    }
  }
}
      } else { // recording file does not exist
        tmrpcm.play("no_recording.wav"); // play "no recording" sound
        delay(2000);
      }
    else { // maximum playback count reached
      tmrpcm.play("max_playback_reached.wav"); // play "maximum playback count reached" sound
      delay(2000);
    }
  
  button_press_count = 0;
  neopixels.setPixelColor(0, 0, 0, 0);
  neopixels.show();
  if (tmrpcm.isPlaying()) {
    tmrpcm.stopPlayback();
  }
  playback_active = false;
  recording_active = false;

 else { // recording file does not exist
        tmrpcm.play("no_recording.wav"); // play "no recording" sound
        delay(2000);
      }
     else { // maximum playback count reached
      tmrpcm.play("max_playback_reached.wav"); // play "maximum playback count reached" sound
      delay(2000);
    }
  
  button_press_count = 0;
  neopixels.setPixelColor(0, 0, 0, 0);
  neopixels.show();
  if (tmrpcm.isPlaying()) {
    tmrpcm.stopPlayback();
  }
  playback_active = false;
  recording_active = false;

  // check if auto-delete timer has expired and delete recording if necessary
  if (auto_delete_timer > 0 && millis() - auto_delete_timer >= AUTO_DELETE_TIMER) {
    SD.remove("recording.wav");
    auto_delete_timer = 0;
    tmrpcm.play("recording_deleted.wav"); // play "recording deleted" sound
    delay(2000);
  }

  // check if playback is finished
  if (playback_active && !tmrpcm.isPlaying()) {
    playback_active = false;
    playback_count++;
    auto_delete_timer = millis();
    tmrpcm.play("playback_complete.wav"); // play "playback complete" sound
    delay(2000);
  }

      if (recording_active) { // if recording is active
        if (button_press_time > 0 && millis() - button_press_time >= MIN_RECORDING_TIME) { // if button has been held for minimum recording time
          button_press_time = 0;
          recording_active = false;
          audioRecorder.finishRecording();
          tmrpcm.play("recording_finished.wav"); // play "recording finished" sound
          delay(2000);
          playback_count = 0; // reset playback count
          auto_delete_timer = millis() + AUTO_DELETE_DELAY; // set auto delete timer
        }
      } else if (playback_active) { // if playback is active
        if (!tmrpcm.isPlaying()) { // if sound finished playing
          playback_active = false;
        }
      }
    
  


