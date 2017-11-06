import RPi.GPIO as GPIO
import time, datetime
from time import sleep
import os, sys
import logging
import re

import requests


# use P1 header pin numbering convention
GPIO.setmode(GPIO.BOARD)

# Set up the GPIO channels - one input and one output
GPIO.setup(8, GPIO.IN)

##############################


def TriggerAlarm():
        r = requests.get("http://192.168.1.3:8090/?cameras=always&alarms=yes")
        if 200 == r.status_code:
                return True
        else:
                return False

def MuteAlarm():
        r = requests.get("http://192.168.1.3:8090/?cameras=never&alarms=yes")
        if 200 == r.status_code:
                return True
        else:
                return False

def GetUserForcedStop():
        r = requests.get("http://192.168.1.3:8090")
        if 200 == r.status_code:
                m = re.search(r"type='radio' name='cameras' id='camerasOver' value='mouseover' checked='checked'", r.content)
                if m:
                        return True
        return False


def GetTime():
        millis = int(round(time.time() * 1000))
        return millis

LOG_FILE = "/home/pi/test/mylog.txt"
TARGET_DIR = "/home/pi/test/log/"
time_text = datetime.datetime.now().strftime("%Y-%m-%d-%H-%M-%S   ")
cmd = "mv {0} {1}".format(LOG_FILE, TARGET_DIR+time_text[:-3]+"_log.txt")
print cmd
os.system(cmd)

LOG_LEVEL = logging.INFO
LOG_FORMAT = "%(asctime)s %(levelname)s %(message)s"
logging.basicConfig(filename=LOG_FILE, format=LOG_FORMAT, level=LOG_LEVEL)

time_stamp = 3600000
log_text = 'starting...'
time_text = ''

prev_state = 0
curr_state = 0
#BLOCKING_MODE_LENGTH_IN_SEC = 30
FORCED_STOP = False
prev_msg = ""

sleep(10)

start_time = GetTime()

if GetUserForcedStop():
        FORCED_STOP = True
        log_text += ' forced stop enabled => DisArm'
elif MuteAlarm():
        log_text += ' reset alarm at start up'

current_time = start_time
try:
        while current_time > 3600000:
                prev_state = curr_state

                if prev_msg == log_text:
                        pass
                else:
                        print time_text+log_text
                        logging.info(log_text)
                        prev_msg = log_text

                sleep(1)

                current_time = GetTime()
                time_text = datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S   ")

                #condition check DisArm
                if time_text[-5:] == "00   ":
                        if GetUserForcedStop():
                                FORCED_STOP = True
                                log_text = 'forced stop enabled => DisArm'
                        else:
                                FORCED_STOP = False
                                log_text = 'forced stop disabled => Arm'
                        continue

                #condition forced stop
                if FORCED_STOP:
                        log_text = 'alarm is blocked due to forced stop'
                        continue

                #condition startup
                if current_time - start_time < 60*1000:
                        log_text = 'starting up delay, alarm blocking mode'
                        continue

                #condition trigger
                if 1 == GPIO.input(8):
                        log_text = 'GPIO pin is on. '
                        curr_state = 1

                        if 0 == prev_state:
                                if TriggerAlarm():
                                        log_text += " post successfully to website"
                                        time_stamp = current_time
                                else:
                                        log_text += " post to website failed"
                        continue

                #condition normal
                else:
                        log_text = 'actively detecting, normal mode. '
                        curr_state = 0

                        #subcondition trigger => normal
                        if 1 == prev_state:
                                if MuteAlarm():
                                        log_text += " post successfully to website"
                                else:
                                        log_text += " post to website failed"

except KeyboardInterrupt:
    pass

raw_input('Press Enter')

GPIO.cleanup()
