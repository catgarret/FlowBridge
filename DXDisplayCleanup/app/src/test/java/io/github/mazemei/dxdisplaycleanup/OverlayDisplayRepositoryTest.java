package io.github.mazemei.dxdisplaycleanup;

import org.junit.Test;

import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class OverlayDisplayRepositoryTest {
    @Test
    public void emptyValuesAreInactive() {
        assertFalse(OverlayDisplayRepository.hasOverlayValue(null));
        assertFalse(OverlayDisplayRepository.hasOverlayValue(""));
        assertFalse(OverlayDisplayRepository.hasOverlayValue("   "));
        assertFalse(OverlayDisplayRepository.hasOverlayValue("none"));
        assertFalse(OverlayDisplayRepository.hasOverlayValue(" NONE "));
        assertFalse(OverlayDisplayRepository.hasOverlayValue("null"));
    }

    @Test
    public void overlaySpecificationsAreActive() {
        assertTrue(OverlayDisplayRepository.hasOverlayValue(
                "1600x900/150,hdmi"));
        assertTrue(OverlayDisplayRepository.hasOverlayValue(
                "1920x1080/240"));
    }
}
