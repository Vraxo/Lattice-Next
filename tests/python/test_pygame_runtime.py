"""Headless environment probe, not a generated-program acceptance test."""

import os
import unittest

os.environ["SDL_VIDEODRIVER"] = "dummy"
os.environ["SDL_AUDIODRIVER"] = "dummy"
os.environ["PYGAME_HIDE_SUPPORT_PROMPT"] = "1"

import pygame


class PygameRuntimeTests(unittest.TestCase):
    def test_headless_display_input_drawing_timing_and_shutdown(self):
        self.addCleanup(pygame.quit)
        pygame.init()
        self.assertEqual(pygame.version.ver, "2.6.1")
        screen = pygame.display.set_mode([640, 480])
        self.assertEqual(pygame.display.get_driver(), "dummy")
        self.assertEqual(screen.get_size(), (640, 480))
        clock = pygame.time.Clock()
        for _ in range(3):
            pygame.event.pump()
            keys = pygame.key.get_pressed()
            self.assertFalse(keys[pygame.K_w])
            screen.fill([0, 0, 0])
            pygame.draw.rect(screen, [255, 255, 255], [20, 210, 10, 60])
            pygame.draw.circle(screen, [255, 255, 255], [320, 240], 8)
            pygame.display.flip()
            self.assertIsInstance(clock.tick(60), int)
        self.assertEqual(tuple(screen.get_at((20, 210)))[:3], (255, 255, 255))
        self.assertEqual(tuple(screen.get_at((0, 0)))[:3], (0, 0, 0))
        pygame.event.clear()
        self.assertTrue(pygame.event.post(pygame.event.Event(pygame.QUIT)))
        self.assertEqual([event.type for event in pygame.event.get()], [pygame.QUIT])
        pygame.quit()
        self.assertFalse(pygame.get_init())
        self.assertFalse(pygame.display.get_init())


if __name__ == "__main__":
    unittest.main()
